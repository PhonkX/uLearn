using Ionic.Zip;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using log4net;
using uLearn.Model.Blocks;

namespace uLearn
{
	public class CourseManager
	{
		private const string helpPackageName = "Help";

		private static readonly ILog log = LogManager.GetLogger(typeof(CourseManager));

		private readonly DirectoryInfo stagedDirectory;
		private readonly DirectoryInfo coursesDirectory;
		private readonly DirectoryInfo coursesVersionsDirectory;

		private readonly ConcurrentDictionary<string, Course> courses = new ConcurrentDictionary<string, Course>(StringComparer.InvariantCultureIgnoreCase);
		/* LRU-cache for course versions. 50 is a capactiy of the cache. */
		private readonly LruCache<Guid, Course> versionsCache = new LruCache<Guid, Course>(50);

		private static readonly CourseLoader loader = new CourseLoader();

		public CourseManager(DirectoryInfo baseDirectory)
			: this(
				baseDirectory.GetSubdir("Courses.Staging"),
				baseDirectory.GetSubdir("Courses.Versions"),
				baseDirectory.GetSubdir("Courses")
				  )
		{
		}

		public CourseManager(DirectoryInfo stagedDirectory, DirectoryInfo coursesVersionsDirectory, DirectoryInfo coursesDirectory)
		{
			this.stagedDirectory = stagedDirectory;
			this.coursesDirectory = coursesDirectory;
			this.coursesVersionsDirectory = coursesVersionsDirectory;
		}

		public IEnumerable<Course> GetCourses()
		{
			LoadCoursesIfNotYet();
			return courses.Values;
		}

		public virtual Course GetCourse(string courseId)
		{
			LoadCoursesIfNotYet();
			return courses.Get(courseId);
		}

		public Course GetVersion(Guid versionId)
		{
			Course version;
			if (versionsCache.TryGet(versionId, out version))
				return version;

			var versionFile = GetCourseVersionFile(versionId);
			version = LoadCourseFromZip(versionFile);

			/* Add version to cache for fast loading next time */
			versionsCache.Add(versionId, version);
			return version;
		}

		public FileInfo GetStagingCourseFile(string courseId)
		{
			var packageName = GetPackageName(courseId);
			if (Path.GetInvalidFileNameChars().Any(packageName.Contains))
				throw new Exception(courseId);
			return stagedDirectory.GetFile(packageName);
		}

		public DirectoryInfo GetExtractedCourseDirectory(string courseId)
		{
			return coursesDirectory.GetSubdir(courseId);
		}

		public DirectoryInfo GetExtractedVersionDirectory(Guid versionId)
		{
			return GetExtractedCourseDirectory(Utils.GetNormalizedGuid(versionId));
		}

		public FileInfo GetCourseVersionFile(Guid versionId)
		{
			var packageName = GetPackageName(versionId);
			return coursesVersionsDirectory.GetFile(packageName);
		}

		public string GetStagingCoursePath(string courseId)
		{
			return GetStagingCourseFile(courseId).FullName;
		}

		private static readonly object reloadLock = new object();

		private void LoadCoursesIfNotYet()
		{
			Exception firstException = null;
			lock (reloadLock)
			{
				if (courses.Count != 0)
					return;
				log.Info($"Загружаю курсы из {stagedDirectory.FullName}");
				var courseZips = stagedDirectory.GetFiles("*.zip");
				foreach (var zipFile in courseZips)
				{
					log.Info($"Обновляю курс из {zipFile.Name}");
					try
					{
						var courseId = GetCourseId(zipFile.FullName);
						ReloadCourse(courseId);
					}
					catch (Exception e)
					{
						log.Error($"Не могу загрузить курс из {zipFile.FullName}", e);
						if (firstException == null)
							firstException = new Exception("Error loading course from " + zipFile.Name, e);
					}
				}
			}
			if (firstException != null)
				throw firstException;
		}

		protected Course ReloadCourse(string courseId)
		{
			/* First try load course from directory */
			try
			{
				var courseDir = GetExtractedCourseDirectory(courseId);
				log.Info($"Сначала попробую загрузить уже распакованный курс из {courseDir.FullName}");
				return ReloadCourseFromDirectory(courseDir);
			}
			catch (Exception)
			{
				var zipFile = GetStagingCourseFile(courseId);
				log.Info($"Неудача. Загружаю из zip-архива: {zipFile.FullName}");
				return ReloadCourseFromZip(zipFile);
			}
		}

		private Course ReloadCourseFromZip(FileInfo zipFile)
		{
			var course = LoadCourseFromZip(zipFile);
			courses[course.Id] = course;
			log.Info($"Курс {course.Id} загружен из {zipFile.FullName} и сохранён в памяти");
			return course;
		}

		private Course ReloadCourseFromDirectory(DirectoryInfo directory)
		{
			var course = LoadCourseFromDirectory(directory);
			courses[course.Id] = course;
			log.Info($"Курс {course.Id} загружен из {directory.FullName} и сохранён в памяти");
			return course;
		}

		private static void ClearDirectory(DirectoryInfo directory, bool deleteDirectory = false)
		{
			foreach (var file in directory.GetFiles())
				file.Delete();
			foreach (var subDirectory in directory.GetDirectories())
			{
				/* subDirectory.Delete(true) sometimes not works */
				ClearDirectory(subDirectory);
				subDirectory.Delete();
			}
			if (deleteDirectory)
				directory.Delete();
		}

		private void UnzipFile(FileInfo zipFile, DirectoryInfo unpackDirectory)
		{
			using (var zip = ZipFile.Read(zipFile.FullName, new ReadOptions { Encoding = Encoding.GetEncoding(866) }))
			{
				ClearDirectory(unpackDirectory);
				zip.ExtractAll(unpackDirectory.FullName, ExtractExistingFileAction.OverwriteSilently);
			}
		}

		private DirectoryInfo UnzipCourseFile(FileInfo zipFile)
		{
			var courseOrVersionId = GetCourseId(zipFile.Name);
			var courseDir = coursesDirectory.CreateSubdirectory(courseOrVersionId);
			log.Info($"Распаковываю архив с курсом из {zipFile.FullName} в {courseDir.FullName}");
			UnzipFile(zipFile, courseDir);
			return courseDir;
		}

		public Course LoadCourseFromZip(FileInfo zipFile)
		{
			var courseDir = UnzipCourseFile(zipFile);
			return LoadCourseFromDirectory(courseDir);
		}

		public Course LoadCourseFromDirectory(DirectoryInfo dir)
		{
			WaitWhileCourseIsLocked(GetCourseId(dir.Name));
			return loader.LoadCourse(dir);
		}

		public static string GetCourseId(string packageName)
		{
			return Path.GetFileNameWithoutExtension(packageName);
		}

		public string GetPackageName(string courseId)
		{
			return courseId + ".zip";
		}

		public string GetPackageName(Guid versionId)
		{
			return Utils.GetNormalizedGuid(versionId) + ".zip";
		}

		public DateTime GetLastWriteTime(string courseId)
		{
			return stagedDirectory.GetFile(GetPackageName(courseId)).LastWriteTime;
		}

		public bool TryCreateCourse(string courseId)
		{
			if (courseId.Any(GetInvalidCharacters().Contains))
				return false;

			var package = stagedDirectory.GetFile(GetPackageName(courseId));
			if (package.Exists)
				return true;

			var helpPackage = stagedDirectory.GetFile(GetPackageName(helpPackageName));
			if (!helpPackage.Exists)
				CreateEmptyCourse(courseId, package.FullName);
			else
				CreateCourseFromExample(courseId, package.FullName, helpPackage);

			ReloadCourseFromZip(package);
			return true;
		}

		public void EnsureVersionIsExtracted(Guid versionId)
		{
			var versionDirectory = GetExtractedVersionDirectory(versionId);
			if (!versionDirectory.Exists)
			{
				Directory.CreateDirectory(versionDirectory.FullName);
				UnzipFile(GetCourseVersionFile(versionId), versionDirectory);
			}
		}

		private static void CreateEmptyCourse(string courseId, string path)
		{
			using (var zip = new ZipFile(Encoding.GetEncoding(866)))
			{
				zip.AddEntry("Course.xml",
					string.Format(
						"<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n" +
						"<Course xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"https://ulearn.azurewebsites.net/course\">\n" +
						"\t<title>{0}</title>\n" +
						"</Course>",
						courseId),
					Encoding.UTF8);
				zip.Save(path);
			}
		}

		private static void CreateCourseFromExample(string courseId, string path, FileInfo helpPackage)
		{
			helpPackage.CopyTo(path, true);
			var nsResolver = new XmlNamespaceManager(new NameTable());
			nsResolver.AddNamespace("course", "https://ulearn.azurewebsites.net/course");
			nsResolver.AddNamespace("lesson", "https://ulearn.azurewebsites.net/lesson");
			nsResolver.AddNamespace("quiz", "https://ulearn.azurewebsites.net/quiz");
			using (var zip = ZipFile.Read(path, new ReadOptions { Encoding = Encoding.GetEncoding(866) }))
			{
				if (zip.ContainsEntry("Course.xml"))
					UpdateXmlElement(zip["Course.xml"], "//course:Course/course:title", courseId, zip, nsResolver);
				foreach (var entry in zip.SelectEntries("name = *.lesson.xml").Where(entry => CourseLoader.IsSlideFile(Path.GetFileName(entry.FileName))))
					UpdateXmlElement(entry, "//lesson:Lesson/lesson:id", Guid.NewGuid().ToString(), zip, nsResolver);
				foreach (var entry in zip.SelectEntries("name = *.quiz.xml").Where(entry => CourseLoader.IsSlideFile(Path.GetFileName(entry.FileName))))
					UpdateXmlAttribute(entry, "//quiz:Quiz", "id", Guid.NewGuid().ToString(), zip, nsResolver);
				foreach (var entry in zip.SelectEntries("name = *.cs").Where(entry => CourseLoader.IsSlideFile(Path.GetFileName(entry.FileName))))
					UpdateCsFiles(entry, Guid.NewGuid().ToString(), zip);
			}
		}

		private static void UpdateXmlElement(ZipEntry entry, string selector, string value, ZipFile zip, IXmlNamespaceResolver nsResolver)
		{
			UpdateXmlEntity(entry, selector, element => element.Value = value, zip, nsResolver);
		}

		private static void UpdateXmlAttribute(ZipEntry entry, string selector, string attribute, string value, ZipFile zip, XmlNamespaceManager nsResolver)
		{
			UpdateXmlEntity(entry, selector, element => element.Attribute(attribute).Value = value, zip, nsResolver);
		}

		private static void UpdateCsFiles(ZipEntry entry, string slideId, ZipFile zip)
		{
			string code;
			using (var entryStream = entry.OpenReader())
			{
				code = new StreamReader(entryStream).ReadToEnd();
			}
			code = Regex.Replace(code, "(?<=\\[Slide\\(\".*\",\\s*\").+(?=\"\\)\\])", slideId);
			zip.UpdateEntry(entry.FileName, code, Encoding.UTF8);
			zip.Save();
		}

		private static void UpdateXmlEntity(ZipEntry entry, string selector, Action<XElement> update, ZipFile zip, IXmlNamespaceResolver nsResolver)
		{
			var output = new MemoryStream();
			using (var entryStream = entry.OpenReader())
			{
				var xml = XDocument.Load(entryStream);
				update(xml.XPathSelectElement(selector, nsResolver));
				xml.Save(output);
			}
			zip.UpdateEntry(entry.FileName, output.GetBuffer());
			zip.Save();
		}

		public bool HasPackageFor(string courseId)
		{
			return GetStagingCourseFile(courseId).Exists;
		}

		public static char[] GetInvalidCharacters()
		{
			return new[] { '&' }.Concat(Path.GetInvalidFileNameChars()).Concat(Path.GetInvalidPathChars()).Distinct().ToArray();
		}

		public Course FindCourseBySlideById(Guid slideId)
		{
			return GetCourses().FirstOrDefault(c => c.Slides.Count(s => s.Id == slideId) > 0);
		}

		public void UpdateCourse(Course course)
		{
			if (!courses.ContainsKey(course.Id))
				return;
			courses[course.Id] = course;
		}

		private readonly TimeSpan waitBetweenLockTries = TimeSpan.FromSeconds(0.1);
		private readonly TimeSpan lockLifeTime = TimeSpan.FromMinutes(20);
		private int updateCourseEachOperarionTriesCount = 5;

		private FileInfo GetCourseLockFile(string courseId)
		{
			return coursesDirectory.GetFile("~" + courseId + ".lock");
		}

		private static bool TryCreateLockFile(FileInfo lockFile)
		{
			var tempFileName = Path.GetTempFileName();
			try
			{
				new FileInfo(tempFileName).MoveTo(lockFile.FullName);
				return true;
			}
			catch (IOException)
			{
				return false;
			}
		}

		public void LockCourse(string courseId)
		{
			var lockFile = GetCourseLockFile(courseId);
			while (true)
			{
				if (TryCreateLockFile(lockFile))
					return;

				log.Info($"Курс {courseId} заблокирован, жду {waitBetweenLockTries.TotalSeconds} секунд");

				Thread.Sleep(waitBetweenLockTries);

				try
				{
					lockFile.Refresh();
					/* If lock-file has been created ago, just delete it and unzip course again */
					if (lockFile.Exists && lockFile.LastWriteTime < DateTime.Now.Subtract(lockLifeTime))
					{
						log.Info($"Курс {courseId} заблокирован слишком давно, снимаю блокировку");

						lockFile.Delete();
						UnzipCourseFile(GetStagingCourseFile(courseId));
					}
				}
				catch (IOException)
				{
				}
			}
		}

		public void ReleaseCourse(string courseId)
		{
			GetCourseLockFile(courseId).Delete();
		}

		public void WaitWhileCourseIsLocked(string courseId)
		{
			LockCourse(courseId);
			ReleaseCourse(courseId);
		}

		private void TrySeveralTimes(Action function)
		{
			Exception lastException = null;
			for (var tryNumber = 1; tryNumber <= updateCourseEachOperarionTriesCount; tryNumber++)
			{
				try
				{
					function.Invoke();
					return;
				}
				catch (Exception e)
				{
					lastException = e;
				}
			}
			if (lastException != null)
				throw lastException;
		}

		public void MoveCourse(Course course, DirectoryInfo sourceDirectory, DirectoryInfo destinationDirectory)
		{
			var tempDirectoryName = coursesDirectory.GetSubdir(Path.GetRandomFileName());
			LockCourse(course.Id);

			try
			{

				TrySeveralTimes(() => Directory.Move(destinationDirectory.FullName, tempDirectoryName.FullName));

				try
				{
					TrySeveralTimes(() => Directory.Move(sourceDirectory.FullName, destinationDirectory.FullName));
				}
				catch (IOException)
				{
					/* In case of any file system's error rollback previous operation */
					TrySeveralTimes(() => Directory.Move(tempDirectoryName.FullName, destinationDirectory.FullName));
					throw;
				}
				FixFileReferencesInCourse(course, sourceDirectory, destinationDirectory);

				UpdateCourse(course);
			}
			finally
			{
				ReleaseCourse(course.Id);
			}
			TrySeveralTimes(() => ClearDirectory(tempDirectoryName, true));
		}

		private void FixFileReferencesInCourse(Course course, DirectoryInfo sourceDirectory, DirectoryInfo destinationDirectory)
		{
			foreach (var instructorNote in course.InstructorNotes)
				instructorNote.File = (FileInfo)GetNewPathForFileAfterMoving(instructorNote.File, sourceDirectory, destinationDirectory);
			foreach (var slide in course.Slides)
			{
				slide.Info.SlideFile = (FileInfo)GetNewPathForFileAfterMoving(slide.Info.SlideFile, sourceDirectory, destinationDirectory);

				foreach (var exerciseBlock in slide.Blocks.OfType<ProjectExerciseBlock>())
					exerciseBlock.SlideFolderPath = (DirectoryInfo)GetNewPathForFileAfterMoving(exerciseBlock.SlideFolderPath, sourceDirectory, destinationDirectory);
			}
		}

		private FileSystemInfo GetNewPathForFileAfterMoving(FileSystemInfo file, DirectoryInfo sourceDirectory, DirectoryInfo destinationDirectory)
		{
			if (!file.IsInDirectory(sourceDirectory))
				return file;

			var relativePath = file.GetRelativePath(sourceDirectory.FullName);
			var newPath = Path.Combine(destinationDirectory.FullName, relativePath);

			if (file is DirectoryInfo)
				return new DirectoryInfo(newPath);
			return new FileInfo(newPath);
		}
	}
}