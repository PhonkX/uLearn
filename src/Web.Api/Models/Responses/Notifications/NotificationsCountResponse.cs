﻿using System;
using System.Runtime.Serialization;
using Ulearn.Common.Api.Models.Responses;

namespace Ulearn.Web.Api.Models.Responses.Notifications
{
	[DataContract]
	public class NotificationsCountResponse : SuccessResponse
	{
		[DataMember(Name = "count")]
		public int Count { get; set; }

		[DataMember(Name = "last_timestamp")]
		public DateTime? LastTimestamp { get; set; }
	}
}