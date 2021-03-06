using System.Runtime.Serialization;
using Ulearn.Common.Api.Models.Responses;

namespace Ulearn.Web.Api.Models.Responses.Comments
{
	[DataContract]
	public class CreateCommentResponse : SuccessResponse
	{
		[DataMember(Name = "id")]
		public int CommentId { get; set; }
		
		[DataMember(Name = "api_url")]
		public string ApiUrl { get; set; }
	}
}