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
using System.Web;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Diagnostics;

using Microsoft.AspNetCore.Http;

namespace SplendidCRM
{
	public class SchedulerUtils
	{
		private static bool bInsideTimer = false;
		// 11/08/2022 Paul.  Separate Archive timer. 
		public static bool bInsideArchiveTimer = false;
		// 11/02/2022 Paul.  Keep track of last job for verbose logging. 
		private static string sLastJob = String.Empty;

		public static string[] Jobs = new string[]
			{ "pollMonitoredInboxes"
			, "runMassEmailCampaign"
			, "pruneDatabase"
			, "pollMonitoredInboxesForBouncedCampaignEmails"
			, "BackupDatabase"
			, "BackupTransactionLog"
			, "CleanSystemLog"
			, "CleanSystemSyncLog"
			, "CheckVersion"
			, "pollOutboundEmails"
			, "RunAllArchiveRules"    // 02/17/2018 Paul.  ModulesArchiveRules module to Professional. 
			, "RunExternalArchive"    // 04/10/2018 Paul.  Run External Archive. 
			};

		private DbProviderFactories  DbProviderFactories = new SplendidCRM.DbProviderFactories();
		private HttpApplicationState Application         = new HttpApplicationState();
		private AppSettings          AppSettings         = new AppSettings();
		private HttpContext          Context            ;
		private HttpSessionState     Session            ;
		private Security             Security           ;
		private Sql                  Sql                ;
		private Utils                Utils              ;
		private SqlProcs             SqlProcs           ;
		private SplendidError        SplendidError      ;
		private SyncError            SyncError          ;
		private SplendidCache        SplendidCache      ;
		private SplendidInit         SplendidInit       ;
		private EmailUtils           EmailUtils         ;
		private ArchiveExternalDB    ArchiveExternalDB  ;
		private IBackgroundTaskQueue taskQueue          ;

		// Sync services
		private ExchangeSync                                      ExchangeSync       ;
		private GoogleSync                                        GoogleSync         ;
		private iCloudSync                                        iCloudSync         ;
		private Spring.Social.HubSpot.HubSpotSync                 HubSpotSync        ;
		private Spring.Social.iContact.iContactSync               iContactSync       ;
		private Spring.Social.ConstantContact.ConstantContactSync ConstantContactSync;
		private Spring.Social.GetResponse.GetResponseSync         GetResponseSync    ;
		private Spring.Social.Marketo.MarketoSync                 MarketoSync        ;

		public SchedulerUtils(IHttpContextAccessor httpContextAccessor, HttpSessionState Session, Security Security, Sql Sql, SqlProcs SqlProcs, Utils Utils, SplendidError SplendidError, SyncError SyncError, SplendidCache SplendidCache, SplendidInit SplendidInit, EmailUtils EmailUtils, ArchiveExternalDB ArchiveExternalDB, IBackgroundTaskQueue taskQueue
			, ExchangeSync                                      ExchangeSync       
			, GoogleSync                                        GoogleSync         
			, iCloudSync                                        iCloudSync         
			, Spring.Social.HubSpot.HubSpotSync                 HubSpotSync        
			, Spring.Social.iContact.iContactSync               iContactSync       
			, Spring.Social.ConstantContact.ConstantContactSync ConstantContactSync
			, Spring.Social.GetResponse.GetResponseSync         GetResponseSync    
			, Spring.Social.Marketo.MarketoSync                 MarketoSync        
			)
		{
			this.Context             = httpContextAccessor.HttpContext;
			this.Session             = Session            ;
			this.Security            = Security           ;
			this.Sql                 = Sql                ;
			this.Utils               = Utils              ;
			this.SqlProcs            = SqlProcs           ;
			this.SplendidError       = SplendidError      ;
			this.SyncError           = SyncError          ;
			this.SplendidCache       = SplendidCache      ;
			this.SplendidInit        = SplendidInit       ;
			this.EmailUtils          = EmailUtils         ;
			this.ArchiveExternalDB   = ArchiveExternalDB  ;
			this.taskQueue           = taskQueue          ;

			this.ExchangeSync        = ExchangeSync       ;
			this.GoogleSync          = GoogleSync         ;
			this.iCloudSync          = iCloudSync         ;
			this.HubSpotSync         = HubSpotSync        ;
			this.iContactSync        = iContactSync       ;
			this.ConstantContactSync = ConstantContactSync;
			this.GetResponseSync     = GetResponseSync    ;
			this.MarketoSync         = MarketoSync        ;
		}

		#region CronDescription
		/// <summary>
		/// CronDescription
		/// </summary>
		public string CronDescription(L10N L10n, string sCRON)
		{
			StringBuilder sb = new StringBuilder();
			sCRON = sCRON.Replace(" ", "");
			if ( sCRON == "*::*::*::*::*" )
				return L10n.Term("Schedulers.LBL_OFTEN");
			// 01/28/2009 Paul.  Catch any processing errors during Cron processing. 
			try
			{
				CultureInfo culture = CultureInfo.CreateSpecificCulture(L10n.NAME);
				string sCRON_MONTH       = "*";
				string sCRON_DAYOFMONTH  = "*";
				string sCRON_DAYOFWEEK   = "*";
				string sCRON_HOUR        = "*";
				string sCRON_MINUTE      = "*";
				string[] arrCRON         = sCRON.Replace("::", "|").Split('|');
				string[] arrCRON_TEMP    = new string[] {};
				string[] arrCRON_VALUE   = new string[] {};
				string[] arrDaySuffixes  = new string[32];
				int    nCRON_VALUE       = 0;
				int    nCRON_VALUE_START = 0;
				int    nCRON_VALUE_END   = 0;
				int    nON_THE_MINUTE    = -1;
				for ( int n = 0; n < arrDaySuffixes.Length; n++ )
					arrDaySuffixes[n] = "th";
				arrDaySuffixes[0] = "";
				arrDaySuffixes[1] = "st";
				arrDaySuffixes[2] = "nd";
				arrDaySuffixes[3] = "rd";

				// minute  hour  dayOfMonth  month  dayOfWeek
				if ( arrCRON.Length > 0 ) sCRON_MINUTE     = arrCRON[0];
				if ( arrCRON.Length > 1 ) sCRON_HOUR       = arrCRON[1];
				if ( arrCRON.Length > 2 ) sCRON_DAYOFMONTH = arrCRON[2];
				if ( arrCRON.Length > 3 ) sCRON_MONTH      = arrCRON[3];
				if ( arrCRON.Length > 4 ) sCRON_DAYOFWEEK  = arrCRON[4];
				if ( sCRON_MINUTE != "*" )
				{
					arrCRON_TEMP = sCRON_MINUTE.Split(',');
					// 12/31/2007 Paul.  Check for either comma or dash. 
					if ( sCRON_MINUTE.Split(",-".ToCharArray()).Length == 1 )
					{
						nON_THE_MINUTE = Sql.ToInteger(sCRON_MINUTE);
						sb.Append(L10n.Term("Schedulers.LBL_ON_THE"));
						if ( nON_THE_MINUTE == 0 )
						{
							sb.Append(L10n.Term("Schedulers.LBL_HOUR_SING"));
						}
						else
						{
							sb.Append(nON_THE_MINUTE.ToString("00"));
							sb.Append(L10n.Term("Schedulers.LBL_MIN_MARK"));
						}
					}
					else
					{
						for ( int i = 0, nCronEntries = 0; i < arrCRON_TEMP.Length; i++ )
						{
							if ( arrCRON_TEMP[i].IndexOf('-') >= 0 )
							{
								arrCRON_VALUE = arrCRON_TEMP[i].Split('-');
								if ( arrCRON_VALUE.Length >= 2 )
								{
									nCRON_VALUE_START = Sql.ToInteger(arrCRON_VALUE[0]);
									nCRON_VALUE_END   = Sql.ToInteger(arrCRON_VALUE[1]);
									if ( nCRON_VALUE_START >= 0 && nCRON_VALUE_START <= 23 && nCRON_VALUE_END >= 0 && nCRON_VALUE_END <= 23 )
									{
										if ( nCronEntries > 0 )
											sb.Append(L10n.Term("Schedulers.LBL_AND"));
										sb.Append(L10n.Term("Schedulers.LBL_FROM"));
										sb.Append(L10n.Term("Schedulers.LBL_ON_THE"));
										if ( nCRON_VALUE_START == 0 )
										{
											sb.Append(L10n.Term("Schedulers.LBL_HOUR_SING"));
										}
										else
										{
											sb.Append(nCRON_VALUE_START.ToString("0"));
											sb.Append(L10n.Term("Schedulers.LBL_MIN_MARK"));
										}
										sb.Append(L10n.Term("Schedulers.LBL_RANGE"));
										sb.Append(L10n.Term("Schedulers.LBL_ON_THE"));
										sb.Append(nCRON_VALUE_END.ToString("0"));
										sb.Append(L10n.Term("Schedulers.LBL_MIN_MARK"));
										nCronEntries++;
									}
								}
							}
							else
							{
								nCRON_VALUE = Sql.ToInteger(arrCRON_TEMP[i]);
								if ( nCRON_VALUE >= 0 && nCRON_VALUE <= 23 )
								{
									if ( nCronEntries > 0 )
										sb.Append(L10n.Term("Schedulers.LBL_AND"));
									sb.Append(L10n.Term("Schedulers.LBL_ON_THE"));
									if ( nCRON_VALUE == 0 )
									{
										sb.Append(L10n.Term("Schedulers.LBL_HOUR_SING"));
									}
									else
									{
										sb.Append(nCRON_VALUE.ToString("0"));
										sb.Append(L10n.Term("Schedulers.LBL_MIN_MARK"));
									}
									nCronEntries++;
								}
							}
						}
					}
				}
				if ( sCRON_HOUR != "*" )
				{
					if ( sb.Length > 0 )
						sb.Append("; ");
					arrCRON_TEMP = sCRON_HOUR.Split(',');
					for ( int i = 0, nCronEntries = 0; i < arrCRON_TEMP.Length; i++ )
					{
						if ( arrCRON_TEMP[i].IndexOf('-') >= 0 )
						{
							arrCRON_VALUE = arrCRON_TEMP[i].Split('-');
							if ( arrCRON_VALUE.Length >= 2 )
							{
								nCRON_VALUE_START = Sql.ToInteger(arrCRON_VALUE[0]);
								nCRON_VALUE_END   = Sql.ToInteger(arrCRON_VALUE[1]);
								if ( nCRON_VALUE_START >= 1 && nCRON_VALUE_START <= 31 && nCRON_VALUE_END >= 1 && nCRON_VALUE_END <= 31 )
								{
									if ( nCronEntries > 0 )
										sb.Append(L10n.Term("Schedulers.LBL_AND"));
									sb.Append(L10n.Term("Schedulers.LBL_FROM"));
									sb.Append(arrCRON_VALUE[0]);
									if ( nON_THE_MINUTE >= 0 )
										sb.Append(":" + nON_THE_MINUTE.ToString("00"));
									sb.Append(L10n.Term("Schedulers.LBL_RANGE"));
									sb.Append(arrCRON_VALUE[1]);
									if ( nON_THE_MINUTE >= 0 )
										sb.Append(":" + nON_THE_MINUTE.ToString("00"));
									nCronEntries++;
								}
							}
						}
						else
						{
							nCRON_VALUE = Sql.ToInteger(arrCRON_TEMP[i]);
							if ( nCRON_VALUE >= 1 && nCRON_VALUE <= 31 )
							{
								if ( nCronEntries > 0 )
									sb.Append(L10n.Term("Schedulers.LBL_AND"));
								sb.Append(arrCRON_TEMP[i]);
								if ( nON_THE_MINUTE >= 0 )
									sb.Append(":" + nON_THE_MINUTE.ToString("00"));
								nCronEntries++;
							}
						}
					}
				}
				if ( sCRON_DAYOFMONTH != "*" )
				{
					if ( sb.Length > 0 )
						sb.Append("; ");
					arrCRON_TEMP = sCRON_DAYOFMONTH.Split(',');
					for ( int i = 0, nCronEntries = 0; i < arrCRON_TEMP.Length; i++ )
					{
						if ( arrCRON_TEMP[i].IndexOf('-') >= 0 )
						{
							arrCRON_VALUE = arrCRON_TEMP[i].Split('-');
							if ( arrCRON_VALUE.Length >= 2 )
							{
								nCRON_VALUE_START = Sql.ToInteger(arrCRON_VALUE[0]);
								nCRON_VALUE_END   = Sql.ToInteger(arrCRON_VALUE[1]);
								if ( nCRON_VALUE_START >= 1 && nCRON_VALUE_START <= 31 && nCRON_VALUE_END >= 1 && nCRON_VALUE_END <= 31 )
								{
									if ( nCronEntries > 0 )
										sb.Append(L10n.Term("Schedulers.LBL_AND"));
									sb.Append(L10n.Term("Schedulers.LBL_FROM"));
									sb.Append(nCRON_VALUE_START.ToString() + arrDaySuffixes[nCRON_VALUE_START]);
									sb.Append(L10n.Term("Schedulers.LBL_RANGE"));
									sb.Append(nCRON_VALUE_END.ToString() + arrDaySuffixes[nCRON_VALUE_END]);
									nCronEntries++;
								}
							}
						}
						else
						{
							nCRON_VALUE = Sql.ToInteger(arrCRON_TEMP[i]);
							if ( nCRON_VALUE >= 1 && nCRON_VALUE <= 31 )
							{
								if ( nCronEntries > 0 )
									sb.Append(L10n.Term("Schedulers.LBL_AND"));
								sb.Append(nCRON_VALUE.ToString() + arrDaySuffixes[nCRON_VALUE]);
								nCronEntries++;
							}
						}
					}
				}
				if ( sCRON_MONTH != "*" )
				{
					if ( sb.Length > 0 )
						sb.Append("; ");
					arrCRON_TEMP = sCRON_MONTH.Split(',');
					for ( int i = 0, nCronEntries = 0; i < arrCRON_TEMP.Length; i++ )
					{
						if ( arrCRON_TEMP[i].IndexOf('-') >= 0 )
						{
							arrCRON_VALUE = arrCRON_TEMP[i].Split('-');
							if ( arrCRON_VALUE.Length >= 2 )
							{
								nCRON_VALUE_START = Sql.ToInteger(arrCRON_VALUE[0]);
								nCRON_VALUE_END   = Sql.ToInteger(arrCRON_VALUE[1]);
								if ( nCRON_VALUE_START >= 1 && nCRON_VALUE_START <= 12 && nCRON_VALUE_END >= 1 && nCRON_VALUE_END <= 12 )
								{
									if ( nCronEntries > 0 )
										sb.Append(L10n.Term("Schedulers.LBL_AND"));
									sb.Append(L10n.Term("Schedulers.LBL_FROM"));
									sb.Append(culture.DateTimeFormat.MonthNames[nCRON_VALUE_START]);
									sb.Append(L10n.Term("Schedulers.LBL_RANGE"));
									sb.Append(culture.DateTimeFormat.MonthNames[nCRON_VALUE_END]);
									nCronEntries++;
								}
							}
						}
						else
						{
							nCRON_VALUE = Sql.ToInteger(arrCRON_TEMP[i]);
							if ( nCRON_VALUE >= 1 && nCRON_VALUE <= 12 )
							{
								if ( nCronEntries > 0 )
									sb.Append(L10n.Term("Schedulers.LBL_AND"));
								sb.Append(culture.DateTimeFormat.MonthNames[nCRON_VALUE]);
								nCronEntries++;
							}
						}
					}
				}
				if ( sCRON_DAYOFWEEK != "*" )
				{
					if ( sb.Length > 0 )
						sb.Append("; ");
					arrCRON_TEMP = sCRON_DAYOFWEEK.Split(',');
					for ( int i = 0, nCronEntries = 0; i < arrCRON_TEMP.Length; i++ )
					{
						if ( arrCRON_TEMP[i].IndexOf('-') >= 0 )
						{
							arrCRON_VALUE = arrCRON_TEMP[i].Split('-');
							if ( arrCRON_VALUE.Length >= 2 )
							{
								nCRON_VALUE_START = Sql.ToInteger(arrCRON_VALUE[0]);
								nCRON_VALUE_END   = Sql.ToInteger(arrCRON_VALUE[1]);
								if ( nCRON_VALUE_START >= 0 && nCRON_VALUE_START <= 6 && nCRON_VALUE_END >= 0 && nCRON_VALUE_END <= 6 )
								{
									if ( nCronEntries > 0 )
										sb.Append(L10n.Term("Schedulers.LBL_AND"));
									sb.Append(L10n.Term("Schedulers.LBL_FROM"));
									sb.Append(culture.DateTimeFormat.DayNames[nCRON_VALUE_START]);
									sb.Append(L10n.Term("Schedulers.LBL_RANGE"));
									sb.Append(culture.DateTimeFormat.DayNames[nCRON_VALUE_END]);
									nCronEntries++;
								}
							}
						}
						else
						{
							nCRON_VALUE = Sql.ToInteger(arrCRON_TEMP[i]);
							if ( nCRON_VALUE >= 0 && nCRON_VALUE <= 6 )
							{
								if ( nCronEntries > 0 )
									sb.Append(L10n.Term("Schedulers.LBL_AND"));
								sb.Append(culture.DateTimeFormat.DayNames[nCRON_VALUE]);
								nCronEntries++;
							}
						}
					}
				}
				return sb.ToString();
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				return "<font class=error>" + ex.Message + "</font>";
			}
		}
		#endregion

		// 10/27/2008 Paul.  Pass the context instead of the Application so that more information will be available to the error handling. 
		public void RunJob(string sJOB)
		{
			DbProviderFactory dbf = DbProviderFactories.GetFactory();
			switch ( sJOB )
			{
				case "function::BackupDatabase":
				{
					// 01/28/2008 Paul.  Cannot perform a backup or restore operation within a transaction. BACKUP DATABASE is terminating abnormally.
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						try
						{
							string sFILENAME = String.Empty;
							string sTYPE     = "FULL";
							//SqlProcs.spSqlBackupDatabase(ref sNAME, "FULL", trn);
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandType = CommandType.StoredProcedure;
								cmd.CommandText = "spSqlBackupDatabase";
								// 02/09/2008 Paul.  A database backup can take a long time.  Don't timeout. 
								cmd.CommandTimeout = 0;
								IDbDataParameter parFILENAME = Sql.AddParameter(cmd, "@FILENAME", sFILENAME  , 255);
								IDbDataParameter parTYPE     = Sql.AddParameter(cmd, "@TYPE"    , sTYPE      ,  20);
								parFILENAME.Direction = ParameterDirection.InputOutput;
								cmd.ExecuteNonQuery();
								sFILENAME = Sql.ToString(parFILENAME.Value);
							}
							SplendidError.SystemMessage("Information", new StackTrace(true).GetFrame(0), "Database backup complete " + sFILENAME);
						}
						catch(Exception ex)
						{
							SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
						}
					}
					break;
				}
				case "function::BackupTransactionLog":
				{
					// 01/28/2008 Paul.  Cannot perform a backup or restore operation within a transaction. BACKUP DATABASE is terminating abnormally.
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						try
						{
							string sFILENAME = String.Empty;
							string sTYPE     = "LOG";
							//SqlProcs.spSqlBackupDatabase(ref sNAME, "LOG", trn);
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandType = CommandType.StoredProcedure;
								cmd.CommandText = "spSqlBackupDatabase";
								// 02/09/2008 Paul.  A database backup can take a long time.  Don't timeout. 
								cmd.CommandTimeout = 0;
								IDbDataParameter parFILENAME = Sql.AddParameter(cmd, "@FILENAME", sFILENAME  , 255);
								IDbDataParameter parTYPE     = Sql.AddParameter(cmd, "@TYPE"    , sTYPE      ,  20);
								parFILENAME.Direction = ParameterDirection.InputOutput;
								cmd.ExecuteNonQuery();
								sFILENAME = Sql.ToString(parFILENAME.Value);
							}
							SplendidError.SystemMessage("Information", new StackTrace(true).GetFrame(0), "Transaction Log backup complete " + sFILENAME);
						}
						catch(Exception ex)
						{
							SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
						}
					}
					break;
				}
				case "function::runMassEmailCampaign":
				{
					// 12/30/2007 Paul.  Update the last run date before running so that the date marks the start of the run. 
					EmailUtils.SendQueued(Guid.Empty, Guid.Empty, false);
					break;
				}
				case "function::pruneDatabase"       :
				{
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						// 10/07/2009 Paul.  We need to create our own global transaction ID to support auditing and workflow on SQL Azure, PostgreSQL, Oracle, DB2 and MySQL. 
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								SqlProcs.spSqlPruneDatabase(trn);
								trn.Commit();
							}
							catch(Exception ex)
							{
								trn.Rollback();
								SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
							}
						}
					}
					break;
				}
				// 02/26/2010 Paul.  Allow system log to be cleaned. 
				case "function::CleanSystemLog"       :
				{
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								// SqlProcs.spSYSTEM_LOG_Cleanup(trn);
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									// 02/26/2010 Paul.  If the database is very old, then the first cleanup can take a long time. 
									cmd.Transaction    = trn;
									cmd.CommandType    = CommandType.StoredProcedure;
									cmd.CommandText    = "spSYSTEM_LOG_Cleanup";
									cmd.CommandTimeout = 0;
									cmd.ExecuteNonQuery();
								}
								trn.Commit();
							}
							catch(Exception ex)
							{
								trn.Rollback();
								SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
							}
						}
						// 09/22/2010 Paul.  We need to cleanup the WORKFLOW_EVENTS table on the Professional Edition. 
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								// SqlProcs.spWORKFLOW_EVENTS_ProcessAll(trn);
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.Transaction    = trn;
									cmd.CommandType    = CommandType.StoredProcedure;
									cmd.CommandText    = "spWORKFLOW_EVENTS_ProcessAll";
									cmd.CommandTimeout = 0;
									cmd.ExecuteNonQuery();
								}
								trn.Commit();
							}
							catch(Exception ex)
							{
								trn.Rollback();
								SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
							}
						}
					}
					break;
				}
				// 03/27/2010 Paul.  Allow system log to be cleaned. 
				case "function::CleanSystemSyncLog"   :
				{
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								// SqlProcs.spSYSTEM_SYNC_LOG_Cleanup(trn);
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									// 02/26/2010 Paul.  If the database is very old, then the first cleanup can take a long time. 
									cmd.Transaction    = trn;
									cmd.CommandType    = CommandType.StoredProcedure;
									cmd.CommandText    = "spSYSTEM_SYNC_LOG_Cleanup";
									cmd.CommandTimeout = 0;
									cmd.ExecuteNonQuery();
								}
								trn.Commit();
							}
							catch(Exception ex)
							{
								trn.Rollback();
								SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
							}
						}
					}
					break;
				}
				case "function::pollMonitoredInboxes":
				{
					EmailUtils.CheckMonitored(Guid.Empty);
					break;
				}
				case "function::pollMonitoredInboxesForBouncedCampaignEmails":
				{
					EmailUtils.CheckBounced(Guid.Empty);
					break;
				}
				case "function::CheckVersion":
				{
					try
					{
						DataTable dtVersions = Utils.CheckVersion();

						DataView vwVersions = dtVersions.DefaultView;
						vwVersions.RowFilter = "New = '1'";
						if ( vwVersions.Count > 0 )
						{
							Application["available_version"            ] = Sql.ToString(vwVersions[0]["Build"      ]);
							Application["available_version_description"] = Sql.ToString(vwVersions[0]["Description"]);
						}
					}
					catch(Exception ex)
					{
						SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
					}
					break;
				}
				case "function::pollOutboundEmails":
				{
					// 05/15/2008 Paul.  Check for outbound emails. 
					EmailUtils.SendOutbound();
					break;
				}
				case "function::OfflineClientSync":
				{
					Application["SystemSync.LastBackgroundSync"] = DateTime.Now;
					// 05/22/2011 Paul.  We need to catch any exceptions as a failure in a thread will abort the entire session. 
					try
					{
						// 11/21/2009 Paul.  This is an Offline Client scheduled task. It cannot be configured on the server as the SCHEDULES table is not sync'd.
						SyncUtils.Retrieve(false, false);
						SyncUtils.Sync    (false, false);
						SyncUtils.Send    ();
					}
					catch(Exception ex)
					{
						SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
					}
					break;
				}
				// 04/06/2010 Paul.  Add Exchange function. 
				case "function::pollExchangeSync":
				{
					// 01/21/2021 Paul.  Provide a way to disable cloud sevices on QA and UAT instances. 
					if ( !Sql.ToBoolean(Application["CONFIG.DisableCloudServices"]) )
					{
						// 04/25/2010 Paul.  Create a new thread as the sync process can take a long time. 
						taskQueue.QueueBackgroundWorkItemAsync(ExchangeSync.SyncAllUsers);
					}
					break;
				}
				// 11/24/2011 Paul.  Add support for Google Apps. 
				case "function::pollGoogleSync":
				{
					// 01/21/2021 Paul.  Provide a way to disable cloud sevices on QA and UAT instances. 
					if ( !Sql.ToBoolean(Application["CONFIG.DisableCloudServices"]) )
					{
						taskQueue.QueueBackgroundWorkItemAsync(GoogleSync.SyncAllUsers);
					}
					break;
				}
				// 05/13/2012 Paul.  Add support for Apple iCloud. 
				case "function::pollICloudSync":
				{
					// 01/21/2021 Paul.  Provide a way to disable cloud sevices on QA and UAT instances. 
					if ( !Sql.ToBoolean(Application["CONFIG.DisableCloudServices"]) )
					{
						taskQueue.QueueBackgroundWorkItemAsync(iCloudSync.SyncAllUsers);
					}
					break;
				}
				// 04/27/2015 Paul.  Add support for HubSpot. 
				case "function::pollHubSpot":
				{
					// 01/21/2021 Paul.  Provide a way to disable cloud sevices on QA and UAT instances. 
					if ( !Sql.ToBoolean(Application["CONFIG.DisableCloudServices"]) )
					{
						taskQueue.QueueBackgroundWorkItemAsync(HubSpotSync.Sync);
					}
					break;
				}
				// 05/02/2015 Paul.  Add support for iContact. 
				case "function::polliContact":
				{
					// 01/21/2021 Paul.  Provide a way to disable cloud sevices on QA and UAT instances. 
					if ( !Sql.ToBoolean(Application["CONFIG.DisableCloudServices"]) )
					{
						taskQueue.QueueBackgroundWorkItemAsync(iContactSync.Sync);
					}
					break;
				}
				// 05/04/2015 Paul.  Add support for ConstantContact. 
				case "function::pollConstantContact":
				{
					// 01/21/2021 Paul.  Provide a way to disable cloud sevices on QA and UAT instances. 
					if ( !Sql.ToBoolean(Application["CONFIG.DisableCloudServices"]) )
					{
						taskQueue.QueueBackgroundWorkItemAsync(ConstantContactSync.Sync);
					}
					break;
				}
				// 05/06/2015 Paul.  Add support for GetResponse. 
				case "function::pollGetResponse":
				{
					// 01/21/2021 Paul.  Provide a way to disable cloud sevices on QA and UAT instances. 
					if ( !Sql.ToBoolean(Application["CONFIG.DisableCloudServices"]) )
					{
						taskQueue.QueueBackgroundWorkItemAsync(GetResponseSync.Sync);
					}
					break;
				}
				// 05/15/2015 Paul.  Add support for Marketo. 
				case "function::pollMarketo":
				{
					// 01/21/2021 Paul.  Provide a way to disable cloud sevices on QA and UAT instances. 
					if ( !Sql.ToBoolean(Application["CONFIG.DisableCloudServices"]) )
					{
						taskQueue.QueueBackgroundWorkItemAsync(MarketoSync.Sync);
					}
					break;
				}
				// 02/17/2018 Paul.  ModulesArchiveRules module to Professional. 
				case "function::RunAllArchiveRules":
				{
					// 07/10/2018 Paul.  Don't run normal archive rules if external archive is enabled. 
					// 10/27/2022 Paul.  Just now adding to Professional. 
					if ( Sql.IsEmptyString(Application["ArchiveConnectionString"]) )
					{
						using ( IDbConnection con = dbf.CreateConnection() )
						{
							con.Open();
							using ( IDbTransaction trn = Sql.BeginTransaction(con) )
							{
								try
								{
									using ( IDbCommand cmd = con.CreateCommand() )
									{
										cmd.Transaction    = trn;
										cmd.CommandType    = CommandType.StoredProcedure;
										cmd.CommandText    = "spMODULES_ARCHIVE_RULES_RunAll";
										cmd.CommandTimeout = 0;
										cmd.ExecuteNonQuery();
									}
									trn.Commit();
								}
								catch(Exception ex)
								{
									trn.Rollback();
									SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
								}
							}
						}
					}
					else
					{
						SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), "SchedulerUtils.RunJobs: Rules cannot be run manually when External Archive is enabled.");
					}
					break;
				}
				// 04/10/2018 Paul.  Run External Archive. 
				case "function::RunExternalArchive":
				{
					taskQueue.QueueBackgroundWorkItemAsync(ArchiveExternalDB.RunArchive);
					break;
				}
			}
		}

		// 10/27/2008 Paul.  Pass the context instead of the Application so that more information will be available to the error handling. 
		public void OnTimer()
		{
			// 12/22/2007 Paul.  In case the timer takes a long time, only allow one timer event to be processed. 
			if ( !bInsideTimer )
			{
				bInsideTimer = true;
				try
				{
					DbProviderFactory dbf = DbProviderFactories.GetFactory();
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						string sSQL ;
						DateTime dtLastUpdate = Sql.ToDateTime(Application["SYSTEM_EVENTS.MaxDate"]);
						if ( dtLastUpdate == DateTime.MinValue )
						{
							dtLastUpdate = DateTime.Now;
							// 02/24/2009 Paul.  Update app variable so that we will know when the last update ran. 
							Application["SYSTEM_EVENTS.MaxDate"] = dtLastUpdate;
						}
						
						// 08/20/2008 Paul.  We reload the system data if a system table or cached table changes. 
						// The primary reason we do this is to support a load-balanced system where changes 
						// on one server need to be replicated to the cache of the other servers. 
						sSQL = "select TABLE_NAME                  " + ControlChars.CrLf
						     + "  from vwSYSTEM_EVENTS             " + ControlChars.CrLf
						     + " where DATE_ENTERED > @DATE_ENTERED" + ControlChars.CrLf
						     + " group by TABLE_NAME               " + ControlChars.CrLf
						     + " order by TABLE_NAME               " + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@DATE_ENTERED", dtLastUpdate);
							using ( DataTable dt = new DataTable() )
							{
								using ( DbDataAdapter da = dbf.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									da.Fill(dt);
									if ( dt.Rows.Count > 0 )
									{
										cmd.Parameters.Clear();
										sSQL = "select max(DATE_ENTERED)" + ControlChars.CrLf
										     + "  from vwSYSTEM_EVENTS  " + ControlChars.CrLf;
										cmd.CommandText = sSQL;
										dtLastUpdate = Sql.ToDateTime(cmd.ExecuteScalar());
										Application["SYSTEM_EVENTS.MaxDate"] = dtLastUpdate;

										StringBuilder sbTables = new StringBuilder();
										foreach ( DataRow row in dt.Rows )
										{
											if ( sbTables.Length > 0 )
												sbTables.Append(", ");
											sbTables.Append(Sql.ToString(row["TABLE_NAME"]));
										}
										// 03/02/2009 Paul.  We must pass the context to the error handler. 
										SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), "System Events: " + sbTables.ToString());
										SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), "System Events Last Update on " + dtLastUpdate.ToString());

										foreach ( DataRow row in dt.Rows )
										{
											string sTABLE_NAME = Sql.ToString(row["TABLE_NAME"]);
											SplendidCache.ClearTable(sTABLE_NAME);
											// 10/26/2008 Paul.  IIS7 Integrated Pipeline does not allow HttpContext access inside Application_Start. 
											if ( sTABLE_NAME.StartsWith("TERMINOLOGY") )
												SplendidInit.InitTerminology();
											else if ( sTABLE_NAME == "MODULES" || sTABLE_NAME.StartsWith("ACL_") )
												SplendidInit.InitModuleACL();
											else if ( sTABLE_NAME == "CONFIG" )
												SplendidInit.InitConfig();
											else if ( sTABLE_NAME == "TIMEZONES" )
												SplendidInit.InitTimeZones();
											else if ( sTABLE_NAME == "CURRENCIES" )
												SplendidInit.InitCurrencies();
										}
									}
								}
							}
						}
						// 10/13/2008 Paul.  Clear out old system events so that future queries are fast. 
						// 10/07/2009 Paul.  We need to create our own global transaction ID to support auditing and workflow on SQL Azure, PostgreSQL, Oracle, DB2 and MySQL. 
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.Transaction = trn;
									cmd.CommandType = CommandType.StoredProcedure;
									cmd.CommandText = "spSYSTEM_EVENTS_ProcessAll";
									cmd.ExecuteNonQuery();
								}
								trn.Commit();
							}
							catch(Exception ex)
							{
								trn.Rollback();
								SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
							}
						}
					}

					// 12/30/2007 Paul.  Workflow events always get processed. 
					// 07/26/2008 Paul.  Provide a way to disable workflow. 
					bool bEnableWorkflow = Sql.ToBoolean(Application["CONFIG.enable_workflow"]);
					if ( bEnableWorkflow )
						WorkflowUtils.Process();
					
					// 01/27/2009 Paul.  If multiple apps connect to the same database, make sure that only one is the job server. 
					// This is primarily for load-balanced sites. 
					int nSplendidJobServer = Sql.ToInteger(Application["SplendidJobServer"]);
					if ( nSplendidJobServer == 0 )
					{
						string sSplendidJobServer = AppSettings["SplendidJobServer"];
						// 09/17/2009 Paul.  If we are running in Azure, then assume that this is the only instance. 
						string sMachineName = sSplendidJobServer;
						try
						{
							// 09/17/2009 Paul.  Azure does not support MachineName.  Just ignore the error. 
							sMachineName = System.Environment.MachineName;
						}
						catch
						{
						}
						if ( Sql.IsEmptyString(sSplendidJobServer) || String.Compare(sMachineName, sSplendidJobServer, true) == 0 )
						{
							nSplendidJobServer = 1;
							SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), sMachineName + " is a Splendid Job Server.");
						}
						else
						{
							nSplendidJobServer = -1;
							SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), sMachineName + " is not a Splendid Job Server.");
						}
						Application["SplendidJobServer"] = nSplendidJobServer;
					}
					if ( nSplendidJobServer > 0 )
					{
						using ( DataTable dt = new DataTable() )
						{
							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								string sSQL ;
								sSQL = "select *               " + ControlChars.CrLf
								     + "  from vwSCHEDULERS_Run" + ControlChars.CrLf
								     + " order by NEXT_RUN     " + ControlChars.CrLf;
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.CommandText = sSQL;
									// 01/01/2008 Paul.  The scheduler query should always be very fast. 
									// In the off chance that there is a problem, abort after 15 seconds. 
									cmd.CommandTimeout = 15;

									using ( DbDataAdapter da = dbf.CreateDataAdapter() )
									{
										((IDbDataAdapter)da).SelectCommand = cmd;
										da.Fill(dt);
									}
								}
							}
							// 05/14/2009 Paul.  Provide a way to track scheduler events. 
							if ( !Sql.ToBoolean(Application["CONFIG.suppress_scheduler_warning"]) )
							{
								SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), "Scheduler Jobs to run: " + dt.Rows.Count.ToString() );
							}
							// 01/13/2008 Paul.  Loop outside the connection so that only one connection will be used. 
							foreach ( DataRow row in dt.Rows )
							{
								Guid     gID        = Sql.ToGuid    (row["ID"      ]);
								string   sJOB       = Sql.ToString  (row["JOB"     ]);
								// 01/31/2008 Paul.  Next run becomes last run. 
								DateTime dtLAST_RUN = Sql.ToDateTime(row["NEXT_RUN"]);
								// 11/08/2022 Paul.  Separate Archive timer. 
								if ( Sql.ToBoolean(Application["CONFIG.Archive.SeparateTimer"]) )
								{
									if ( sJOB == "function::RunAllArchiveRules" || sJOB == "function::RunExternalArchive" )
									{
										break;
									}
								}
								// 11/02/2022 Paul.  Keep track of last job for verbose logging. 
								sLastJob = sJOB;
								try
								{
									if ( !Sql.ToBoolean(Application["CONFIG.suppress_scheduler_warning"]) )
									{
										SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), "Scheduler Job Start: " + sJOB + " at " + dtLAST_RUN.ToString() );
									}
									RunJob(sJOB);
									if ( !Sql.ToBoolean(Application["CONFIG.suppress_scheduler_warning"]) )
									{
										SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), "Scheduler Job End: " + sJOB + " at " + DateTime.Now.ToString() );
									}
								}
								finally
								{
									using ( IDbConnection con = dbf.CreateConnection() )
									{
										con.Open();
										// 10/07/2009 Paul.  We need to create our own global transaction ID to support auditing and workflow on SQL Azure, PostgreSQL, Oracle, DB2 and MySQL. 
										using ( IDbTransaction trn = Sql.BeginTransaction(con) )
										{
											try
											{
												// 01/12/2008 Paul.  Make sure the Last Run value is updated after the operation.
												SqlProcs.spSCHEDULERS_UpdateLastRun(gID, dtLAST_RUN, trn);
												trn.Commit();
											}
											catch(Exception ex)
											{
												trn.Rollback();
												SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
											}
										}
									}
								}
							}
						}
					}
				}
				catch(Exception ex)
				{
					SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
				}
				finally
				{
					bInsideTimer = false;
				}
			}
			// 11/02/2022 Paul.  Keep track of last job for verbose logging. 
			else if ( !Sql.ToBoolean(Application["CONFIG.Scheduler.Verbose"]) )
			{
				SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), "Scheduler Busy: " + sLastJob );
			}
		}

		// 11/08/2022 Paul.  Separate Archive timer. 
		public void OnArchiveTimer()
		{
			if ( !bInsideArchiveTimer )
			{
				bInsideArchiveTimer = true;
				try
				{
					int nSplendidJobServer = Sql.ToInteger(Application["SplendidJobServer"]);
					if ( nSplendidJobServer == 0 )
					{
						string sSplendidJobServer = AppSettings["SplendidJobServer"];
						string sMachineName = sSplendidJobServer;
						try
						{
							// 09/17/2009 Paul.  Azure does not support MachineName.  Just ignore the error. 
							sMachineName = System.Environment.MachineName;
						}
						catch
						{
						}
						if ( Sql.IsEmptyString(sSplendidJobServer) || String.Compare(sMachineName, sSplendidJobServer, true) == 0 )
						{
							nSplendidJobServer = 1;
							SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), sMachineName + " is a Splendid Job Server.");
						}
						else
						{
							nSplendidJobServer = -1;
							SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), sMachineName + " is not a Splendid Job Server.");
						}
						Application["SplendidJobServer"] = nSplendidJobServer;
					}
					if ( nSplendidJobServer > 0 )
					{
						using ( DataTable dt = new DataTable() )
						{
							DbProviderFactory dbf = DbProviderFactories.GetFactory();
							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								string sSQL ;
								sSQL = "select *               " + ControlChars.CrLf
								     + "  from vwSCHEDULERS_Run" + ControlChars.CrLf
								     + " where JOB in ('function::RunAllArchiveRules', 'function::RunExternalArchive')" + ControlChars.CrLf
								     + " order by NEXT_RUN     " + ControlChars.CrLf;
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.CommandText = sSQL;
									cmd.CommandTimeout = 15;
									using ( DbDataAdapter da = dbf.CreateDataAdapter() )
									{
										((IDbDataAdapter)da).SelectCommand = cmd;
										da.Fill(dt);
									}
								}
							}
							if ( !Sql.ToBoolean(Application["CONFIG.suppress_scheduler_warning"]) )
							{
								SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), "Archive Jobs to run: " + dt.Rows.Count.ToString() );
							}
							foreach ( DataRow row in dt.Rows )
							{
								Guid     gID        = Sql.ToGuid    (row["ID"      ]);
								string   sJOB       = Sql.ToString  (row["JOB"     ]);
								DateTime dtLAST_RUN = Sql.ToDateTime(row["NEXT_RUN"]);
								// 11/02/2022 Paul.  Keep track of last job for verbose logging. 
								sLastJob = sJOB;
								try
								{
									if ( !Sql.ToBoolean(Application["CONFIG.suppress_scheduler_warning"]) )
									{
										SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), "Archive Job Start: " + sJOB + " at " + dtLAST_RUN.ToString() );
									}
									RunJob(sJOB);
									if ( !Sql.ToBoolean(Application["CONFIG.suppress_scheduler_warning"]) )
									{
										SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), "Archive Job End: " + sJOB + " at " + DateTime.Now.ToString() );
									}
								}
								finally
								{
									using ( IDbConnection con = dbf.CreateConnection() )
									{
										con.Open();
										using ( IDbTransaction trn = Sql.BeginTransaction(con) )
										{
											try
											{
												SqlProcs.spSCHEDULERS_UpdateLastRun(gID, dtLAST_RUN, trn);
												trn.Commit();
											}
											catch(Exception ex)
											{
												trn.Rollback();
												SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
											}
										}
									}
								}
							}
						}
					}
				}
				catch(Exception ex)
				{
					SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
				}
				finally
				{
					bInsideArchiveTimer = false;
				}
			}
			// 11/02/2022 Paul.  Keep track of last job for verbose logging. 
			else if ( !Sql.ToBoolean(Application["CONFIG.Scheduler.Verbose"]) )
			{
				SplendidError.SystemMessage("Warning", new StackTrace(true).GetFrame(0), "Archive Jobs Busy: " + sLastJob );
			}
		}

	}
}
