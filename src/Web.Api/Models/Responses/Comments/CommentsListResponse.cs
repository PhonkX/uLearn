using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Ulearn.Common.Api.Models.Responses;
using Ulearn.Web.Api.Models.Common;

namespace Ulearn.Web.Api.Models.Responses.Comments
{
	[DataContract]
	public class CommentsListResponse : PaginatedResponse
	{
		[DataMember(Name = "comments")]
		public List<CommentResponse> TopLevelComments { get; set; }
	}

	[DataContract]
	public class CommentResponse
	{
		[DataMember(Name = "id")]
		public int Id { get; set; }
		
		[DataMember(Name = "author")]
		public ShortUserInfo Author { get; set; }
		
		[DataMember(Name = "text")]
		public string Text { get; set; }
		
		[DataMember(Name = "rendered_text")]
		public string RenderedText { get; set; }
		
		[DataMember(Name = "publish_time")]
		public DateTime PublishTime { get; set; }
		
		[DataMember(Name = "is_approved")]
		public bool IsApproved { get; set; }

		[DataMember(Name = "is_correct_answer", EmitDefaultValue = false)]
		public bool? IsCorrectAnswer { get; set; }

		[DataMember(Name = "is_pinned_to_top", EmitDefaultValue = false)]
		public bool? IsPinnedToTop { get; set; }

		[DataMember(Name = "likes_count")]
		public int LikesCount { get; set; }
		
		[DataMember(Name = "replies", EmitDefaultValue = false)]
		public List<CommentResponse> Replies { get; set; }
		
		[DataMember(Name = "course_id", EmitDefaultValue = false)]
		public string CourseId { get; set; }
		
		[DataMember(Name = "slide_id", EmitDefaultValue = false)]
		public Guid? SlideId { get; set; }
		
		[DataMember(Name = "reply_to", EmitDefaultValue = false)]
		public int? ParentCommentId { get; set; }
	}
}