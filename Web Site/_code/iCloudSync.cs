/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *
 * Any use of the contents of this file are subject to the SplendidCRM Professional Source Code License 
 * Agreement, or other written agreement between you and SplendidCRM ("License"). By installing or 
 * using this file, you have unconditionally agreed to the terms and conditions of the License, 
 * including but not limited to restrictions on the number of users therein, and you may not use this 
 * file except in compliance with the License. 
 * 
 * SplendidCRM owns all proprietary rights, including all copyrights, patents, trade secrets, and 
 * trademarks, in and to the contents of this file.  You will not link to or in any way combine the 
 * contents of this file or any derivatives with any Open Source Code in any manner that would require 
 * the contents of this file to be made available to any third party. 
 * 
 * IN NO EVENT SHALL SPLENDIDCRM BE RESPONSIBLE FOR ANY DAMAGES OF ANY KIND, INCLUDING ANY DIRECT, 
 * SPECIAL, PUNITIVE, INDIRECT, INCIDENTAL OR CONSEQUENTIAL DAMAGES.  Other limitations of liability 
 * and disclaimers set forth in the License. 
 * 
 *********************************************************************************************************************/
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;

using Microsoft.AspNetCore.Http;

namespace SplendidCRM
{
	[DataContract]
	public class AppleAccessToken
	{
		[DataMember] public string token_type    { get; set; }
		[DataMember] public string expires_in    { get; set; }
		[DataMember] public string access_token  { get; set; }
		[DataMember] public string refresh_token { get; set; }
		[DataMember] public string id_token      { get; set; }

		public string AccessToken
		{
			get { return access_token;  }
			set { access_token = value; }
		}
		public string RefreshToken
		{
			get { return refresh_token;  }
			set { refresh_token = value; }
		}
		public Int64 ExpiresInSeconds
		{
			get { return Sql.ToInt64(expires_in);  }
			set { expires_in = Sql.ToString(value); }
		}
		public string TokenType
		{
			get { return token_type;  }
			set { token_type = value; }
		}
	}

	public class iCloudSync
	{
		public iCloudSync()
		{
		}

		public static bool Validate_iCloud(HttpApplicationState Application, string sICLOUD_USERNAME, string sICLOUD_PASSWORD, StringBuilder sbErrors)
		{
			return false;
		}

		public static void AcquireAccessToken(HttpContext Context, Guid gUSER_ID, string sCode, string sIdToken, StringBuilder sbErrors)
		{
		}

		public static AppleAccessToken RefreshAccessToken(HttpContext Context, Guid gUSER_ID, bool bForceRefresh)
		{
			return null;
		}

#pragma warning disable CS1998
		public async ValueTask SyncAllUsers(CancellationToken token)
		{
		}
#pragma warning restore CS1998

		public class UserSync
		{
			public void Start()
			{
			}

			public static UserSync Create(HttpContext Context, Guid gUSER_ID, bool bSyncAll)
			{
				iCloudSync.UserSync User = null;
				return User;
			}
		}
	}
}
