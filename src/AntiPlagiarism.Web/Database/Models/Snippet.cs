﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AntiPlagiarism.Api.Models;

namespace AntiPlagiarism.Web.Database.Models
{
	public class Snippet
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }
		
		[Required]
		public int TokensCount { get; set; }
		
		[Required]
		public SnippetType SnippetType { get; set; } 
		
		[Required]
		public int Hash { get; set; }
		
		/* This property is equal to amount of authors whom solutions has this shippet.
		 * It shows "hotness" of this snippet
		 */
		[Required]
		public int AuthorsCount { get; set; }

		public override string ToString()
		{
			return $"SnippetWith{SnippetType.ToString()}(Hash={Hash}, TokensCount={TokensCount})";
		}
	}
}