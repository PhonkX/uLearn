﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AntiPlagiarism.Api.Models;
using AntiPlagiarism.Web.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace AntiPlagiarism.Web.Database.Repos
{
	public interface ISubmissionsRepo
	{
		Task<Submission> FindSubmissionByIdAsync(int submissionId);
		Task<List<Submission>> GetSubmissionsByIdsAsync(IEnumerable<int> submissionIds);
		Task<Submission> AddSubmissionAsync(int clientId, Guid taskId, Guid authorId, Language language, string code, int tokensCount, string additionalInfo);
		/* TODO (andgein): add filter by clientId to following methods */
		Task<List<Guid>> GetLastAuthorsByTaskAsync(Guid taskId, int count);
		Task<List<Submission>> GetLastSubmissionsByAuthorsForTaskAsync(Guid taskId, IEnumerable<Guid> authorsIds);
		Task<List<Submission>> GetSubmissionsByAuthorAndTaskAsync(Guid authorId, Guid taskId, int count);
	}

	public class SubmissionsRepo : ISubmissionsRepo
	{
		private readonly AntiPlagiarismDb db;

		public SubmissionsRepo(AntiPlagiarismDb db)
		{
			this.db = db;
		}

		public Task<Submission> FindSubmissionByIdAsync(int submissionId)
		{
			return db.Submissions.Include(s => s.Program).FirstOrDefaultAsync(s => s.Id == submissionId);
		}
		
		public Task<List<Submission>> GetSubmissionsByIdsAsync(IEnumerable<int> submissionIds)
		{
			return db.Submissions.Include(s => s.Program).Where(s => submissionIds.Contains(s.Id)).ToListAsync();
		}

		public async Task<Submission> AddSubmissionAsync(int clientId, Guid taskId, Guid authorId, Language language, string code, int tokensCount, string additionalInfo="")
		{
			var submission = new Submission
			{
				ClientId = clientId,
				TaskId = taskId,
				AuthorId = authorId,
				Language = language,
				Program = new Code
				{
					Text = code,
				},
				TokensCount = tokensCount,
				AdditionalInfo = additionalInfo,
				AddingTime = DateTime.Now,
			};

			await db.Submissions.AddAsync(submission);
			await db.SaveChangesAsync();

			return submission;
		}

		public Task<List<Guid>> GetLastAuthorsByTaskAsync(Guid taskId, int count)
		{
			return db.Submissions
				.OrderByDescending(s => s.Id)
				.Where(s => s.TaskId == taskId)
				.Select(s => s.AuthorId)
				.Distinct()
				.Take(count)
				.ToListAsync();
		}

		public async Task<List<Submission>> GetLastSubmissionsByAuthorsForTaskAsync(Guid taskId, IEnumerable<Guid> authorsIds)
		{
			var lastSubmissionByAuthor = await db.Submissions
				.Where(s => s.TaskId == taskId && authorsIds.Contains(s.AuthorId))
				.GroupBy(s => s.AuthorId)
				.ToDictionaryAsync(g => g.Key, g => g.Last());
			return lastSubmissionByAuthor.Values.ToList();
		}

		public Task<List<Submission>> GetSubmissionsByAuthorAndTaskAsync(Guid authorId, Guid taskId, int count)
		{
			return db.Submissions
				.Include(s => s.Program)
				.Where(s => s.AuthorId == authorId && s.TaskId == taskId)
				.OrderByDescending(s => s.AddingTime)
				.Take(count)
				.ToListAsync();
		}
	}
}