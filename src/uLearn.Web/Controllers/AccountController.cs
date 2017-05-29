﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.Mvc;
using Database;
using Database.DataContexts;
using Database.Extensions;
using Database.Models;
using Microsoft.AspNet.Identity;
using uLearn.Web.Extensions;
using uLearn.Web.FilterAttributes;
using uLearn.Web.Models;

namespace uLearn.Web.Controllers
{
	[ULearnAuthorize]
	public class AccountController : Controller
	{
		private readonly ULearnDb db;
		private readonly CourseManager courseManager = WebCourseManager.Instance;

		private UserManager<ApplicationUser> userManager;
		private readonly UsersRepo usersRepo;
		private readonly UserRolesRepo userRolesRepo;
		private readonly GroupsRepo groupsRepo;
		private readonly CertificatesRepo certificatesRepo;
		private readonly VisitsRepo visitsRepo;
		private readonly NotificationsRepo notificationsRepo;

		private readonly string telegramSecret;

		public AccountController()
		{
			db = new ULearnDb();

			userManager = new ULearnUserManager(db);
			usersRepo = new UsersRepo(db);
			userRolesRepo = new UserRolesRepo(db);
			groupsRepo = new GroupsRepo(db, courseManager);
			certificatesRepo = new CertificatesRepo(db, courseManager);
			visitsRepo = new VisitsRepo(db);
			notificationsRepo = new NotificationsRepo(db);

			telegramSecret = WebConfigurationManager.AppSettings["ulearn.telegram.webhook.secret"] ?? "";
		}

		[AllowAnonymous]
		public ActionResult Login(string returnUrl)
		{
			return RedirectToAction("Index", "Login", new { returnUrl });
		}

		[ULearnAuthorize(MinAccessLevel = CourseRole.Instructor)]
		public ActionResult List(UserSearchQueryModel queryModel)
		{
			return View(queryModel);
		}

		[ChildActionOnly]
		public ActionResult ListPartial(UserSearchQueryModel queryModel)
		{
			var userRoles = usersRepo.FilterUsers(queryModel, userManager);
			var model = GetUserListModel(userRoles);

			return PartialView("_UserListPartial", model);
		}

		private UserListModel GetUserListModel(IEnumerable<UserRolesInfo> users)
		{
			var coursesForUsers = db.UserRoles
				.GroupBy(userRole => userRole.UserId)
				.ToDictionary(
					g => g.Key,
					g => g
						.GroupBy(userRole => userRole.Role)
						.ToDictionary(
							gg => gg.Key,
							gg => gg
								.Select(userRole => userRole.CourseId.ToLower())
								.ToList()
						)
				);

			var courses = User.GetControllableCoursesId().ToList();
			var usersList = users.ToList();

			var model = new UserListModel
			{
				IsCourseAdmin = User.HasAccess(CourseRole.CourseAdmin), 
				ShowDangerEntities = User.IsSystemAdministrator(),
				Users = usersList.Select(user => GetUserModel(user, coursesForUsers, courses)).ToList(),
				UsersGroups = groupsRepo.GetUsersGroupsNamesAsStrings(courses, usersList.Select(u => u.UserId), User)
			};

			return model;
		}

		private UserModel GetUserModel(UserRolesInfo userRoles, Dictionary<string, Dictionary<CourseRole, List<string>>> coursesForUsers, List<string> courses)
		{
			var user = new UserModel(userRoles)
			{
				CoursesAccess = new Dictionary<string, ICoursesAccessListModel>
				{
					{
						LmsRoles.SysAdmin,
						new SingleCourseAccessModel
						{
							HasAccess = userRoles.Roles.Contains(LmsRoles.SysAdmin),
							ToggleUrl = Url.Action("ToggleSystemRole", new { userId = userRoles.UserId, role = LmsRoles.SysAdmin })
						}
					}
				}
			};

			Dictionary<CourseRole, List<string>> coursesForUser;
			if (!coursesForUsers.TryGetValue(userRoles.UserId, out coursesForUser))
				coursesForUser = new Dictionary<CourseRole, List<string>>();

			foreach (var role in Enum.GetValues(typeof(CourseRole)).Cast<CourseRole>().Where(roles => roles != CourseRole.Student))
			{
				user.CoursesAccess[role.ToString()] = new ManyCourseAccessModel
				{
					CoursesAccesses = courses
						.Select(s => new CourseAccessModel
						{
							CourseId = s,
							HasAccess = coursesForUser.ContainsKey(role) && coursesForUser[role].Contains(s.ToLower()),
							ToggleUrl = Url.Action("ToggleRole", new { courseId = s, userId = user.UserId, role })
						})
						.ToList()
				};
			}
			return user;
		}

		private async Task NotifyAbountUserJoinedToGroup(Group group, string userId)
		{
			var notification = new JoinedToYourGroupNotification
			{
				Group = group,
				JoinedUserId = userId
			};
			await notificationsRepo.AddNotification(group.CourseId, notification, userId);
		}

		public async Task<ActionResult> JoinGroup(Guid hash)
		{
			var group = groupsRepo.FindGroupByInviteHash(hash);
			if (group == null)
				return new HttpStatusCodeResult(HttpStatusCode.NotFound);

			if (Request.HttpMethod == "POST")
			{
				await groupsRepo.AddUserToGroup(group.Id, User.Identity.GetUserId());
				await NotifyAbountUserJoinedToGroup(group, User.Identity.GetUserId());

				return Redirect("/");
			}

			return View((object) group.Name);
		}

		[ULearnAuthorize(Roles = LmsRoles.SysAdmin)]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> ToggleSystemRole(string userId, string role)
		{
			if (userId == User.Identity.GetUserId())
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
			if (userManager.IsInRole(userId, role))
				await userManager.RemoveFromRoleAsync(userId, role);
			else
				await userManager.AddToRolesAsync(userId, role);
			return Content(role);
		}

		private async Task NotifyAboutNewInstructor(string courseId, string userId, string initiatedUserId)
		{
			var notification = new AddedInstructorNotification
			{
				AddedUserId = userId,
			};
			await notificationsRepo.AddNotification(courseId, notification, initiatedUserId);
		}

		[ULearnAuthorize(MinAccessLevel = CourseRole.CourseAdmin)]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> ToggleRole(string courseId, string userId, CourseRole role)
		{
			if (userManager.FindById(userId) == null || userId == User.Identity.GetUserId())
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
			var enabledRole = await userRolesRepo.ToggleRole(courseId, userId, role);

			if (enabledRole && (role == CourseRole.Instructor || role == CourseRole.CourseAdmin))
				await NotifyAboutNewInstructor(courseId, userId, User.Identity.GetUserId());

			return Content(role.ToString());
		}

		[HttpPost]
		[ULearnAuthorize(Roles = LmsRoles.SysAdmin)]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> DeleteUser(string userId)
		{
			var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
			if (user != null)
			{
				db.Users.Remove(user);
				await db.SaveChangesAsync();
			}
			return RedirectToAction("List");
		}

		[ULearnAuthorize(MinAccessLevel = CourseRole.Instructor)]
		public ActionResult Info(string userName)
		{
			var user = db.Users.FirstOrDefault(u => u.Id == userName || u.UserName == userName);
			if (user == null)
				return RedirectToAction("List");

			var userCoursesIds = visitsRepo.GetUserCourses(user.Id);
			var userCourses = courseManager.GetCourses().Where(c => userCoursesIds.Contains(c.Id)).ToList();

			var certificates = certificatesRepo.GetUserCertificates(user.Id);

			return View(new UserInfoModel {
				User = user,
				GroupsNames = groupsRepo.GetUserGroupsNamesAsString(userCoursesIds.ToList(), user.Id, User, 10),
				Certificates = certificates,
				Courses = courseManager.GetCourses().ToDictionary(c => c.Id, c => c),
				UserCourses = userCourses,
			});
		}

		[ULearnAuthorize(MinAccessLevel = CourseRole.Instructor)]
		public ActionResult CourseInfo(string userName, string courseId)
		{
			var user = db.Users.FirstOrDefault(u => u.Id == userName || u.UserName == userName);
			if (user == null)
				return RedirectToAction("List");
			var course = courseManager.GetCourse(courseId);
			return View(new UserCourseModel(course, user, db));
		}

		//
		// GET: /Account/Register
		[AllowAnonymous]
		public ActionResult Register(string returnUrl = null)
		{
			return View(new RegisterViewModel { ReturnUrl = returnUrl });
		}

		//
		// POST: /Account/Register
		[HttpPost]
		[AllowAnonymous]
		[ValidateInput(false)]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> Register(RegisterViewModel model)
		{
			if (ModelState.IsValid)
			{
				var user = new ApplicationUser { UserName = model.UserName };
				var result = await userManager.CreateAsync(user, model.Password);
				if (result.Succeeded)
				{
					await AuthenticationManager.LoginAsync(HttpContext, user, isPersistent: false);

					if (string.IsNullOrWhiteSpace(model.ReturnUrl))
						model.ReturnUrl = Url.Action("Index", "Home");
					else
						model.ReturnUrl = this.FixRedirectUrl(model.ReturnUrl);

					model.RegistrationFinished = true;
				}
				else
					this.AddErrors(result);
			}

			return View(model);
		}

		//
		// POST: /Account/Disassociate
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> Disassociate(string loginProvider, string providerKey)
		{
			ManageMessageId? message;
			IdentityResult result =
				await userManager.RemoveLoginAsync(User.Identity.GetUserId(), new UserLoginInfo(loginProvider, providerKey));
			if (result.Succeeded)
			{
				message = ManageMessageId.RemoveLoginSuccess;
			}
			else
			{
				message = ManageMessageId.Error;
			}
			return RedirectToAction("Manage", new { Message = message });
		}

		//
		// GET: /Account/Manage
		public ActionResult Manage(ManageMessageId? message)
		{
			ViewBag.StatusMessage =
				message == ManageMessageId.ChangePasswordSuccess
					? "Пароль был изменен"
					: message == ManageMessageId.SetPasswordSuccess
						? "Пароль установлен"
						: message == ManageMessageId.RemoveLoginSuccess
							? "Внешний логин удален"
							: message == ManageMessageId.Error
								? "Ошибка"
								: "";
			ViewBag.HasLocalPassword = ControllerUtils.HasPassword(userManager, User);
			ViewBag.ReturnUrl = Url.Action("Manage");
			return View();
		}

		//
		// POST: /Account/Manage
		[HttpPost]
		[ValidateInput(false)]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> Manage(ManageUserViewModel model)
		{
			bool hasPassword = ControllerUtils.HasPassword(userManager, User);
			ViewBag.HasLocalPassword = hasPassword;
			ViewBag.ReturnUrl = Url.Action("Manage");
			if (hasPassword)
			{
				if (ModelState.IsValid)
				{
					var result = await userManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword, model.NewPassword);
					if (result.Succeeded)
					{
						return RedirectToAction("Manage", new { Message = ManageMessageId.ChangePasswordSuccess });
					}
					this.AddErrors(result);
				}
			}
			else
			{
				// User does not have a password so remove any validation errors caused by a missing OldPassword field
				ModelState state = ModelState["OldPassword"];
				if (state != null)
				{
					state.Errors.Clear();
				}

				if (ModelState.IsValid)
				{
					IdentityResult result = await userManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);
					if (result.Succeeded)
					{
						return RedirectToAction("Manage", new { Message = ManageMessageId.SetPasswordSuccess });
					}
					else
					{
						this.AddErrors(result);
					}
				}
			}

			// If we got this far, something failed, redisplay form
			return View(model);
		}

		public async Task<ActionResult> StudentInfo()
		{
			var userId = User.Identity.GetUserId();
			var user = await userManager.FindByIdAsync(userId);
			return View(new LtiUserViewModel
			{
				FirstName = user.FirstName,
				LastName = user.LastName,
				Email = user.Email,
			});
		}

		[HttpPost]
		[ValidateInput(false)]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> StudentInfo(LtiUserViewModel userInfo)
		{
			var userId = User.Identity.GetUserId();
			var user = await userManager.FindByIdAsync(userId);
			user.FirstName = userInfo.FirstName;
			user.LastName = userInfo.LastName;
			user.Email = userInfo.Email;
			user.LastEdit = DateTime.Now;
			await userManager.UpdateAsync(user);
			return RedirectToAction("StudentInfo");
		}


		[ChildActionOnly]
		public ActionResult RemoveAccountList()
		{
			var linkedAccounts = userManager.GetLogins(User.Identity.GetUserId());
			ViewBag.ShowRemoveButton = ControllerUtils.HasPassword(userManager, User) || linkedAccounts.Count > 1;
			return PartialView("_RemoveAccountPartial", linkedAccounts);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && userManager != null)
			{
				userManager.Dispose();
				userManager = null;
			}
			base.Dispose(disposing);
		}

		public enum ManageMessageId
		{
			ChangePasswordSuccess,
			SetPasswordSuccess,
			RemoveLoginSuccess,
			Error
		}

		public PartialViewResult ChangeDetailsPartial()
		{
			var user = userManager.FindByName(User.Identity.Name);
			var hasPassword = ControllerUtils.HasPassword(userManager, User);
			var telegramBotName = WebConfigurationManager.AppSettings["ulearn.telegram.botName"];

			var mailTransport = notificationsRepo.FindUsersNotificationTransport<MailNotificationTransport>(user.Id, includeDisabled: true);
			var telegramTransport = notificationsRepo.FindUsersNotificationTransport<TelegramNotificationTransport>(user.Id, includeDisabled: true);

			var courseTitles = courseManager.GetCourses().ToDictionary(c => c.Id, c => c.Title);
			var notificationTypesByCourse = courseTitles.Keys.ToDictionary(c => c, c => notificationsRepo.GetNotificationTypes(User, c));
			var allNotificationTypes = NotificationsRepo.GetAllNotificationTypes();

			var notificationTransportsSettings = courseTitles.Keys.SelectMany(
				c => notificationsRepo.GetNotificationTransportsSettings(c).Select(
					kvp => Tuple.Create(Tuple.Create(c, kvp.Key.Item1, kvp.Key.Item2), kvp.Value.IsEnabled)
				)
			).ToDictionary(kvp => kvp.Item1, kvp => kvp.Item2);

			return PartialView(new UserViewModel
			{
				Name = user.UserName,
				UserId = user.Id, 
				User = user,
				HasPassword = hasPassword,
				FirstName = user.FirstName,
				LastName = user.LastName,
				Email = user.Email,
				TelegramBotName = telegramBotName,

				MailTransport = mailTransport,
				TelegramTransport = telegramTransport,

				CourseTitles = courseTitles,
				AllNotificationTypes = allNotificationTypes,
				NotificationTypesByCourse = notificationTypesByCourse,
				NotificationTransportsSettings = notificationTransportsSettings,
			});
		}

		[HttpPost]
		[ValidateInput(false)]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> ChangeDetailsPartial(UserViewModel userModel)
		{
			var user = await userManager.FindByIdAsync(userModel.UserId);
			if (user == null)
			{
				AuthenticationManager.Logout(HttpContext);
				return RedirectToAction("Index", "Login");
			}
			var nameChanged = user.UserName != userModel.Name;
			if (nameChanged && await userManager.FindByNameAsync(userModel.Name) != null)
				return RedirectToAction("Manage", new { Message = ManageMessageId.Error });
			user.UserName = userModel.Name;
			user.FirstName = userModel.FirstName;
			user.LastName = userModel.LastName;
			user.Email = userModel.Email;
			user.LastEdit = DateTime.Now;
			if (!string.IsNullOrEmpty(userModel.Password))
			{
				await userManager.RemovePasswordAsync(user.Id);
				await userManager.AddPasswordAsync(user.Id, userModel.Password);
			}
			await userManager.UpdateAsync(user);

			if (nameChanged)
			{
				AuthenticationManager.Logout(HttpContext);
				return RedirectToAction("Index", "Login");
			}
			return RedirectToAction("Manage");
		}

		[HttpPost]
		[ULearnAuthorize(Roles = LmsRoles.SysAdmin)]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> ResetPassword(string newPassword, string userId)
		{
			var user = await userManager.FindByIdAsync(userId);
			if (user == null)
				return RedirectToAction("List");
			await userManager.RemovePasswordAsync(userId);
			await userManager.AddPasswordAsync(userId, newPassword);
			return RedirectToAction("Info", new { user.UserName });
		}

		[AllowAnonymous]
		public ActionResult UserMenuPartial()
		{
			var isAuthenticated = Request.IsAuthenticated;
			var user = userManager.FindById(User.Identity.GetUserId());
			var userVisibleName = isAuthenticated ? user.VisibleName : "";
			return PartialView(new UserMenuPartialViewModel
			{
				IsAuthenticated = isAuthenticated,
				UserVisibleName = userVisibleName,
			});
		}

		public async Task<ActionResult> AddTelegram(long chatId, string chatTitle, string hash)
		{
			var correctHash = notificationsRepo.GetSecretHashForTelegramTransport(chatId, chatTitle, telegramSecret);
			if (hash != correctHash)
				return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

			var userId = User.Identity.GetUserId();
			await usersRepo.ChangeTelegram(userId, chatId, chatTitle);
			await notificationsRepo.AddNotificationTransport(new TelegramNotificationTransport
			{
				UserId = userId,
				IsEnabled = true,
			});

			return RedirectToAction("Manage");
		}
	}
}