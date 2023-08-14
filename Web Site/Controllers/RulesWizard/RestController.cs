/*
 * Copyright (C) 2013-2023 SplendidCRM Software, Inc. All Rights Reserved. 
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
 */
using System;
using System.IO;
using System.Xml;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;

namespace SplendidCRM.Controllers.RulesWizard
{
	[Authorize]
	[SplendidSessionAuthorize]
	[ApiController]
	[Route("RulesWizard/Rest.svc")]
	public partial class RestController : ControllerBase
	{
		private IHttpContextAccessor httpContextAccessor;
		private IWebHostEnvironment  hostingEnvironment ;
		private SplendidCRM.DbProviderFactories  DbProviderFactories = new SplendidCRM.DbProviderFactories();
		private HttpApplicationState Application        = new HttpApplicationState();
		private HttpSessionState     Session            ;
		private Security             Security           ;
		private Sql                  Sql                ;
		private L10N                 L10n               ;
		private SplendidCRM.TimeZone TimeZone           = new SplendidCRM.TimeZone();
		private SqlProcs             SqlProcs           ;
		private SplendidError        SplendidError      ;
		private SplendidCache        SplendidCache      ;
		private RestUtil             RestUtil           ;
		private XmlUtil              XmlUtil            ;
		private OrderUtils           OrderUtils         ;
		private QueryBuilder         QueryBuilder       ;
		private SplendidCRM.Crm.Modules          Modules              ;
		private ReportsAttachmentView            ReportsAttachmentView;

		public RestController(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment hostingEnvironment, HttpSessionState Session, Security Security, Sql Sql, SqlProcs SqlProcs, SplendidError SplendidError, SplendidCache SplendidCache, RestUtil RestUtil, XmlUtil XmlUtil, OrderUtils OrderUtils, QueryBuilder QueryBuilder, SplendidCRM.Crm.Modules Modules, ReportsAttachmentView ReportsAttachmentView)
		{
			this.httpContextAccessor = httpContextAccessor;
			this.hostingEnvironment  = hostingEnvironment ;
			this.Session             = Session            ;
			this.Security            = Security           ;
			this.L10n                = new L10N(Sql.ToString(Session["USER_SETTINGS/CULTURE"]));
			this.Sql                 = Sql                ;
			this.SqlProcs            = SqlProcs           ;
			this.SplendidError       = SplendidError      ;
			this.SplendidCache       = SplendidCache      ;
			this.RestUtil            = RestUtil           ;
			this.XmlUtil             = XmlUtil            ;
			this.OrderUtils          = OrderUtils         ;
			this.QueryBuilder        = QueryBuilder       ;
			this.Modules             = Modules            ;
			this.ReportsAttachmentView = ReportsAttachmentView;
		}

		[HttpPost("[action]")]
		public void ValidateRule([FromBody] Dictionary<string, object> dict)
		{
			L10N L10n = new L10N(Sql.ToString(Session["USER_SETTINGS/CULTURE"]));
			if ( !Security.IsAuthenticated() )
			{
				throw(new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS")));
			}
			
			Guid   gRULE_ID      = Guid.NewGuid();
			string sRULE_NAME    = Guid.NewGuid().ToString();
			string sRULE_TYPE    = (dict.ContainsKey("RULE_TYPE"   ) ? Sql.ToString (dict["RULE_TYPE"   ]) : String.Empty);
			int    nPRIORITY     = (dict.ContainsKey("PRIORITY"    ) ? Sql.ToInteger(dict["PRIORITY"    ]) : 0           );
			string sREEVALUATION = (dict.ContainsKey("REEVALUATION") ? Sql.ToString (dict["REEVALUATION"]) : String.Empty);
			bool   bACTIVE       = (dict.ContainsKey("ACTIVE"      ) ? Sql.ToBoolean(dict["ACTIVE"      ]) : true        );
			string sCONDITION    = (dict.ContainsKey("CONDITION"   ) ? Sql.ToString (dict["CONDITION"   ]) : String.Empty);
			string sTHEN_ACTIONS = (dict.ContainsKey("THEN_ACTIONS") ? Sql.ToString (dict["THEN_ACTIONS"]) : String.Empty);
			string sELSE_ACTIONS = (dict.ContainsKey("ELSE_ACTIONS") ? Sql.ToString (dict["ELSE_ACTIONS"]) : String.Empty);
			if ( Sql.IsEmptyString(sRULE_TYPE) )
			{
				throw(new Exception("RULE_TYPE was not specified"));
			}
			
			SplendidRulesTypeProvider typeProvider = new SplendidRulesTypeProvider();
			Type ruleType = typeof(SplendidWizardThis);
			switch ( sRULE_TYPE )
			{
				case "Import"  :  ruleType = typeof(SplendidImportThis );  break;
				// 08/12/2023 Paul.  Should be SplendidReportThis. 
				case "Report"  :  ruleType = typeof(SplendidReportThis );  break;
				case "Business":  ruleType = typeof(SplendidControlThis);  break;
				case "Wizard"  :  ruleType = typeof(SplendidWizardThis );  break;
				default        :  throw(new Exception("Unknown rule type: " + sRULE_TYPE));
			}
			RulesUtil.RulesValidate(gRULE_ID, sRULE_NAME, nPRIORITY, sREEVALUATION, bACTIVE, sCONDITION, sTHEN_ACTIONS, sELSE_ACTIONS, ruleType, typeProvider);
		}

		[HttpPost("[action]")]
		public Guid UpdateModule([FromBody] Dictionary<string, object> dict)
		{
			string sModuleName = "RulesWizard";
			L10N L10n       = new L10N(Sql.ToString(Session["USER_SETTINGS/CULTURE"]));
			int  nACLACCESS = Security.GetUserAccess(sModuleName, "edit");
			if ( !Security.IsAuthenticated() || !Sql.ToBoolean(Application["Modules." + sModuleName + ".RestEnabled"]) || nACLACCESS < 0 )
			{
				// 09/06/2017 Paul.  Include module name in error. 
				throw(new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sModuleName));
			}
			
			string sTableName = Sql.ToString(Application["Modules." + sModuleName + ".TableName"]);
			if ( Sql.IsEmptyString(sTableName) )
				throw(new Exception("Unknown module: " + sModuleName));

			bool bPrimaryKeyOnly   = true ;
			bool bUseSQLParameters = false;
			bool bDesignChart      = false;
			bool bUserSpecific     = false;
			RdlDocument rdl = new RdlDocument(hostingEnvironment, httpContextAccessor, Session, SplendidCache, XmlUtil, String.Empty, String.Empty, bDesignChart);
			Guid   gID                = Guid.Empty  ;
			string sNAME              = String.Empty;
			string sMODULE            = String.Empty;
			string sRELATED           = String.Empty;
			string sRULE_TYPE         = String.Empty;
			Dictionary<string, object> dictFilterXml        = null;
			Dictionary<string, object> dictRelatedModuleXml = null;
			Dictionary<string, object> dictRelationshipXml  = null;
			Dictionary<string, object> dictRulesXml         = null;
			foreach ( string sColumnName in dict.Keys )
			{
				switch ( sColumnName )
				{
					case "ID"               :  gID                  = Sql.ToGuid  (dict[sColumnName]);  break;
					case "NAME"             :  sNAME                = Sql.ToString(dict[sColumnName]);  break;
					// 02/09/2022 Paul.  Keep using MODULE to match Reports. 
					case "MODULE"           :  sMODULE              = Sql.ToString(dict[sColumnName]);  break;
					// 08/12/2023 Paul.  Must keep MODULE_NAME as that is what is used by RulesWizard.EditView. 
					case "MODULE_NAME"      :  sMODULE              = Sql.ToString(dict[sColumnName]);  break;
					case "RELATED"          :  sRELATED             = Sql.ToString(dict[sColumnName]);  break;
					case "RULE_TYPE"        :  sRULE_TYPE           = Sql.ToString(dict[sColumnName]);  break;
					case "filterXml"        :  dictFilterXml        = dict[sColumnName] as Dictionary<string, object>;  break;
					case "relatedModuleXml" :  dictRelatedModuleXml = dict[sColumnName] as Dictionary<string, object>;  break;
					case "relationshipXml"  :  dictRelationshipXml  = dict[sColumnName] as Dictionary<string, object>;  break;
					case "rulesXml"         :  dictRulesXml         = dict[sColumnName] as Dictionary<string, object>;  break;
				}
			}
			if ( Sql.IsEmptyString(sRULE_TYPE) )
			{
				throw(new Exception("RULE_TYPE was not specified"));
			}
			// 05/16/2021 Paul.  Precheck access to filter module. 
			nACLACCESS = Security.GetUserAccess(sMODULE, "edit");
			if ( !Security.IsAuthenticated() || !Sql.ToBoolean(Application["Modules." + sMODULE + ".RestEnabled"]) || nACLACCESS < 0 )
			{
				throw(new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sMODULE));
			}
			rdl.SetCustomProperty             ("Module"        , sMODULE     );
			rdl.SetCustomProperty             ("Related"       , sRELATED    );
			// 06/02/2021 Paul.  React client needs to share code. 
			rdl.SetFiltersCustomProperty      (dictFilterXml       );
			rdl.SetRelatedModuleCustomProperty(dictRelatedModuleXml);
			rdl.SetRelationshipCustomProperty (dictRelationshipXml );
			// 06/02/2021 Paul.  React client needs to share code. 
			DataTable dtRules = RulesUtil.BuildRuleDataTable(dictRulesXml);
			if ( dtRules.Rows.Count == 0 )
			{
				throw(new Exception(L10n.Term("Rules.ERR_NO_RULES")));
			}
			
			Hashtable hashAvailableModules = new Hashtable();
			StringBuilder sbErrors = new StringBuilder();
			string sReportSQL = String.Empty;
			sReportSQL = QueryBuilder.BuildReportSQL(Application, rdl, bPrimaryKeyOnly, bUseSQLParameters, bDesignChart, bUserSpecific, sMODULE, sRELATED, hashAvailableModules, sbErrors);
			if ( sbErrors.Length > 0 )
				throw(new Exception(sbErrors.ToString()));
			
			rdl.SetDataSetFields(hashAvailableModules);
			rdl.SetSingleNode("DataSets/DataSet/Query/CommandText", sReportSQL);
			
			// 12/12/2012 Paul.  For security reasons, we want to restrict the data types available to the rules wizard. 
			SplendidRulesTypeProvider typeProvider = new SplendidRulesTypeProvider();
			Type ruleType = typeof(SplendidWizardThis);
			switch ( sRULE_TYPE )
			{
				case "Import"  :  ruleType = typeof(SplendidImportThis );  break;
				// 08/12/2023 Paul.  Should be SplendidReportThis. 
				case "Report"  :  ruleType = typeof(SplendidReportThis );  break;
				case "Business":  ruleType = typeof(SplendidControlThis);  break;
				case "Wizard"  :  ruleType = typeof(SplendidWizardThis );  break;
				default        :  throw(new Exception("Unknown rule type: " + sRULE_TYPE));
			}
			RuleValidation validation = new RuleValidation(ruleType, typeProvider);
			RuleSet rules = RulesUtil.BuildRuleSet(dtRules, validation);
			
			// 05/17/2021 Paul.  Must set the table name in order to serialize.  Must be Table1. 
			dtRules.TableName = "Table1";
			string sXOML = RulesUtil.Serialize(rules);
			StringBuilder sbRulesXML = new StringBuilder();
			using ( StringWriter wtr = new StringWriter(sbRulesXML, System.Globalization.CultureInfo.InvariantCulture) )
			{
				dtRules.WriteXml(wtr, XmlWriteMode.WriteSchema, false);
			}
			// 06/06/2021 Paul.  Keys may already exist in dictionary, so assign instead. 
			dict["FILTER_SQL"] = sReportSQL           ;
			dict["FILTER_XML"] = rdl.OuterXml         ;
			dict["RULES_XML" ] = sbRulesXML.ToString();

			gID = RestUtil.UpdateTable(sTableName, dict);
			if ( dict.ContainsKey("NAME") )
			{
				string sName = String.Empty;
				if ( dict.ContainsKey("NAME") )
					sName = Sql.ToString(dict["NAME"]);
				try
				{
					if ( !Sql.IsEmptyString(sName) )
						SqlProcs.spTRACKER_Update(Security.USER_ID, sModuleName, gID, sName, "save");
				}
				catch(Exception ex)
				{
					// 04/28/2019 Paul.  There is no compelling reason to send this error to the user. 
					SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				}
			}
			return gID;
		}

		[HttpPost("[action]")]
		public Dictionary<string, object> GetPreviewFilter([FromBody] Dictionary<string, object> dict)
		{
			string sModuleName = "RulesWizard";
			L10N L10n       = new L10N(Sql.ToString(Session["USER_SETTINGS/CULTURE"]));
			int  nACLACCESS = Security.GetUserAccess(sModuleName, "edit");
			if ( !Security.IsAuthenticated() || !Sql.ToBoolean(Application["Modules." + sModuleName + ".RestEnabled"]) || nACLACCESS < 0 )
			{
				// 09/06/2017 Paul.  Include module name in error. 
				throw(new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sModuleName));
			}
			
			string sTableName = Sql.ToString(Application["Modules." + sModuleName + ".TableName"]);
			if ( Sql.IsEmptyString(sTableName) )
				throw(new Exception("Unknown module: " + sModuleName));

			bool bPrimaryKeyOnly   = true ;
			bool bUseSQLParameters = false;
			bool bDesignChart      = false;
			bool bUserSpecific     = false;
			RdlDocument rdl = new RdlDocument(hostingEnvironment, httpContextAccessor, Session, SplendidCache, XmlUtil, String.Empty, String.Empty, bDesignChart);
			Guid   gID                = Guid.Empty  ;
			string sNAME              = String.Empty;
			string sMODULE            = String.Empty;
			string sRELATED           = String.Empty;
			Dictionary<string, object> dictFilterXml        = null;
			Dictionary<string, object> dictRelatedModuleXml = null;
			Dictionary<string, object> dictRelationshipXml  = null;
			int    nSKIP              = Sql.ToInteger(Request.Query["$skip"     ]);
			int    nTOP               = Sql.ToInteger(Request.Query["$top"      ]);
			string sORDER_BY          = Sql.ToString (Request.Query["$orderby"  ]);
			string sSELECT            = Sql.ToString (Request.Query["$select"   ]);
			foreach ( string sColumnName in dict.Keys )
			{
				switch ( sColumnName )
				{
					case "NAME"             :  sNAME                = Sql.ToString(dict[sColumnName]);  break;
					// 02/09/2022 Paul.  Keep using MODULE to match Reports. 
					case "MODULE"           :  sMODULE              = Sql.ToString(dict[sColumnName]);  break;
					// 08/12/2023 Paul.  Must keep MODULE_NAME as that is what is used by RulesWizard.EditView. 
					case "MODULE_NAME"      :  sMODULE              = Sql.ToString(dict[sColumnName]);  break;
					case "RELATED"          :  sRELATED             = Sql.ToString(dict[sColumnName]);  break;
					case "filterXml"        :  dictFilterXml        = dict[sColumnName] as Dictionary<string, object>;  break;
					case "relatedModuleXml" :  dictRelatedModuleXml = dict[sColumnName] as Dictionary<string, object>;  break;
					case "relationshipXml"  :  dictRelationshipXml  = dict[sColumnName] as Dictionary<string, object>;  break;
					case "$skip"            :  nSKIP                = Sql.ToInteger(dict[sColumnName]);  break;
					case "$top"             :  nTOP                 = Sql.ToInteger(dict[sColumnName]);  break;
					case "$orderby"         :  sORDER_BY            = Sql.ToString (dict[sColumnName]);  break;
					case "$select"          :  sSELECT              = Sql.ToString (dict[sColumnName]);  break;
				}
			}
			// 05/16/2021 Paul.  Precheck access to filter module. 
			nACLACCESS = Security.GetUserAccess(sMODULE, "edit");
			if ( !Security.IsAuthenticated() || !Sql.ToBoolean(Application["Modules." + sMODULE + ".RestEnabled"]) || nACLACCESS < 0 )
			{
				throw(new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sMODULE));
			}
			rdl.SetCustomProperty              ("Module"        , sMODULE );
			rdl.SetCustomProperty              ("Related"       , sRELATED);
			// 06/02/2021 Paul.  React client needs to share code. 
			rdl.SetFiltersCustomProperty       (dictFilterXml       );
			rdl.SetRelatedModuleCustomProperty (dictRelatedModuleXml);
			rdl.SetRelationshipCustomProperty  (dictRelationshipXml );
			
			Hashtable hashAvailableModules = new Hashtable();
			StringBuilder sbErrors = new StringBuilder();
			string sReportSQL = String.Empty;
			sReportSQL = QueryBuilder.BuildReportSQL(Application, rdl, bPrimaryKeyOnly, bUseSQLParameters, bDesignChart, bUserSpecific, sMODULE, sRELATED, hashAvailableModules, sbErrors);
			if ( sbErrors.Length > 0 )
				throw(new Exception(sbErrors.ToString()));
			
			long     lTotalCount = 0;
			Guid     gTIMEZONE   = Sql.ToGuid  (Session["USER_SETTINGS/TIMEZONE"]);
			TimeZone T10n        = TimeZone.CreateTimeZone(gTIMEZONE);
			string   sBaseURI    = Request.Scheme + "://" + Request.Host.Host + Request.Path.Value.Replace("/GetModuleList", "/GetModuleItem");
			
			Regex r = new Regex(@"[^A-Za-z0-9_]");
			UniqueStringCollection arrSELECT = new UniqueStringCollection();
			sSELECT = sSELECT.Replace(" ", "");
			if ( !Sql.IsEmptyString(sSELECT) )
			{
				foreach ( string s in sSELECT.Split(',') )
				{
					string sColumnName = r.Replace(s, "");
					if ( !Sql.IsEmptyString(sColumnName) )
						arrSELECT.Add(sColumnName);
				}
			}
			
			string sLastCommand = String.Empty;
			StringBuilder sbDumpSQL = new StringBuilder();
			DataTable dt = new DataTable();
			string sTABLE_NAME = Modules.TableName(sMODULE);
			string sVIEW_NAME = "vw" + sTABLE_NAME + "_List";
			DbProviderFactory dbf = DbProviderFactories.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbCommand cmd = con.CreateCommand() )
				{
					string sSelectSQL = String.Empty;
					if ( arrSELECT != null && arrSELECT.Count > 0 )
					{
						foreach ( string sColumnName in arrSELECT )
						{
							if ( Sql.IsEmptyString(sSelectSQL) )
								sSelectSQL += "select " + sVIEW_NAME + "." + sColumnName + ControlChars.CrLf;
							else
								sSelectSQL += "     , " + sVIEW_NAME + "." + sColumnName + ControlChars.CrLf;
						}
					}
					else
					{
						sSelectSQL = "select " + sVIEW_NAME + ".*" + ControlChars.CrLf;
					}
					cmd.CommandText = sSelectSQL;
					cmd.CommandText += "  from " + sVIEW_NAME + ControlChars.CrLf;
					Security.Filter(cmd, sMODULE, "list");
					if ( !Sql.IsEmptyString(sReportSQL) )
					{
						cmd.CommandText += "   and ID in " + ControlChars.CrLf 
						                + "(" + sReportSQL + ")" + ControlChars.CrLf;
					}
					if ( Sql.IsEmptyString(sORDER_BY.Trim()) )
					{
						sORDER_BY = " order by " + sVIEW_NAME + ".DATE_MODIFIED_UTC" + ControlChars.CrLf;
					}
					else
					{
						r = new Regex(@"[^A-Za-z0-9_, ]");
						sORDER_BY = " order by " + r.Replace(sORDER_BY, "") + ControlChars.CrLf;
					}
					using ( DbDataAdapter da = dbf.CreateDataAdapter() )
					{
						((IDbDataAdapter)da).SelectCommand = cmd;
						dt = new DataTable(sTABLE_NAME);
						if ( nTOP > 0 )
						{
							lTotalCount = -1;
							if ( cmd.CommandText.StartsWith(sSelectSQL) )
							{
								string sOriginalSQL = cmd.CommandText;
								cmd.CommandText = "select count(*) " + ControlChars.CrLf + cmd.CommandText.Substring(sSelectSQL.Length);
								sLastCommand += Sql.ExpandParameters(cmd) + ';' + ControlChars.CrLf;
								lTotalCount = Sql.ToLong(cmd.ExecuteScalar());
								cmd.CommandText = sOriginalSQL;
							}
							if ( nSKIP > 0 )
							{
								int nCurrentPageIndex = nSKIP / nTOP;
								Sql.PageResults(cmd, sTABLE_NAME, sORDER_BY, nCurrentPageIndex, nTOP);
								sLastCommand += Sql.ExpandParameters(cmd);
								da.Fill(dt);
							}
							else
							{
								cmd.CommandText += sORDER_BY;
								using ( DataSet ds = new DataSet() )
								{
									ds.Tables.Add(dt);
									sLastCommand += Sql.ExpandParameters(cmd);
									da.Fill(ds, 0, nTOP, sTABLE_NAME);
								}
							}
						}
						else
						{
							cmd.CommandText += sORDER_BY;
							sLastCommand = Sql.ExpandParameters(cmd);
							da.Fill(dt);
							lTotalCount = dt.Rows.Count;
						}
						sbDumpSQL.Append(sLastCommand);
					}
				}
			}
			
			Dictionary<string, object> dictResponse = RestUtil.ToJson(sBaseURI, sMODULE, dt, T10n);
			dictResponse.Add("__total", lTotalCount);
			if ( Sql.ToBoolean(Application["CONFIG.show_sql"]) )
			{
				dictResponse.Add("__sql", sbDumpSQL.ToString());
			}
			return dictResponse;
		}

		[HttpGet("[action]")]
		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public Dictionary<string, object> GetPreviewRules()
		{
			string sModuleName = "RulesWizard";
			int nACLACCESS = Security.GetUserAccess(sModuleName, "list");
			if ( !Security.IsAuthenticated() || !Sql.ToBoolean(Application["Modules." + sModuleName + ".RestEnabled"]) || nACLACCESS < 0 )
			{
				L10N L10n = new L10N(Sql.ToString(Session["USER_SETTINGS/CULTURE"]));
				// 09/06/2017 Paul.  Include module name in error. 
				throw(new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + Sql.ToString(sModuleName)));
			}
			int    nSKIP            = Sql.ToInteger(Request.Query["$skip"          ]);
			int    nTOP             = Sql.ToInteger(Request.Query["$top"           ]);
			string sORDER_BY        = Sql.ToString (Request.Query["$orderby"       ]);
			string sProcessedFileID = Sql.ToString (Request.Query["ProcessedFileID"]);

			long lTotalCount = 0;
			DataTable dt = new DataTable();
			string sProcessedFileName = Sql.ToString(Session["TempFile." + sProcessedFileID]);
			string sProcessedPathName = Path.Combine(Path.GetTempPath(), sProcessedFileName);
			if ( System.IO.File.Exists(sProcessedPathName) )
			{
				DataSet dsProcessed = new DataSet();
				dsProcessed.ReadXml(sProcessedPathName);
				if ( dsProcessed.Tables.Count == 1 )
				{
					DataTable dtProcessed = dsProcessed.Tables[0];
					DataView vwProcessed = new DataView(dtProcessed);
					if ( Sql.IsEmptyString(sORDER_BY.Trim()) )
					{
						vwProcessed.Sort = "IMPORT_ROW_NUMBER";
					}
					else
					{
						vwProcessed.Sort = sORDER_BY;
					}

					lTotalCount = vwProcessed.Count;
					// 05/23/2020 Paul.  Clone the table, then add the paginated records. 
					dt = dtProcessed.Clone();
					for ( int i = nSKIP; i >= 0 && i < lTotalCount && dt.Rows.Count < nTOP; i++ )
					{
						DataRow row = vwProcessed[i].Row;
						DataRow newRow = dt.NewRow();
						dt.Rows.Add(newRow);
						for ( int j = 0; j < dtProcessed.Columns.Count; j++ )
						{
							newRow[j] = row[j];
						}
					}
				}
			}
			string sBaseURI = Request.Scheme + "://" + Request.Host.Host + Request.Path.Value;
			Guid     gTIMEZONE         = Sql.ToGuid  (Session["USER_SETTINGS/TIMEZONE"]);
			TimeZone T10n              = TimeZone.CreateTimeZone(gTIMEZONE);
			Dictionary<string, object> dictResponse = RestUtil.ToJson(sBaseURI, sModuleName, dt, T10n);
			dictResponse.Add("__total", lTotalCount);
			return dictResponse;
		}

		[HttpPost("[action]")]
		public Dictionary<string, object> SubmitRules([FromBody] Dictionary<string, object> dict)
		{
			string sModuleName = "RulesWizard";
			L10N L10n       = new L10N(Sql.ToString(Session["USER_SETTINGS/CULTURE"]));
			int  nACLACCESS = Security.GetUserAccess(sModuleName, "edit");
			if ( !Security.IsAuthenticated() || !Sql.ToBoolean(Application["Modules." + sModuleName + ".RestEnabled"]) || nACLACCESS < 0 )
			{
				// 09/06/2017 Paul.  Include module name in error. 
				throw(new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sModuleName));
			}
			
			string sTableName = Sql.ToString(Application["Modules." + sModuleName + ".TableName"]);
			if ( Sql.IsEmptyString(sTableName) )
				throw(new Exception("Unknown module: " + sModuleName));

			bool bPrimaryKeyOnly   = true ;
			bool bUseSQLParameters = false;
			bool bDesignChart      = false;
			bool bUserSpecific     = false;
			RdlDocument rdl = new RdlDocument(hostingEnvironment, httpContextAccessor, Session, SplendidCache, XmlUtil, String.Empty, String.Empty, bDesignChart);
			Guid   gID                = Guid.Empty  ;
			string sNAME              = String.Empty;
			string sMODULE            = String.Empty;
			string sRELATED           = String.Empty;
			Guid   gASSIGNED_USER_ID  = Security.USER_ID;
			string sASSIGNED_SET_LIST = String.Empty;
			Guid   gTEAM_ID           = Security.TEAM_ID;
			string sTEAM_SET_LIST     = String.Empty;
			string sTAG_SET_NAME      = String.Empty;
			string sDESCRIPTION       = String.Empty;
			bool   bPreview           = false;
			bool   bUseTransaction    = false;
			Dictionary<string, object> dictFilterXml        = null;
			Dictionary<string, object> dictRelatedModuleXml = null;
			Dictionary<string, object> dictRelationshipXml  = null;
			Dictionary<string, object> dictRulesXml         = null;
			foreach ( string sColumnName in dict.Keys )
			{
				switch ( sColumnName )
				{
					case "NAME"             :  sNAME                = Sql.ToString (dict[sColumnName]);  break;
					// 02/09/2022 Paul.  Keep using MODULE to match Reports. 
					case "MODULE"           :  sMODULE              = Sql.ToString(dict[sColumnName]);  break;
					// 08/12/2023 Paul.  Must keep MODULE_NAME as that is what is used by RulesWizard.EditView. 
					case "MODULE_NAME"      :  sMODULE              = Sql.ToString(dict[sColumnName]);  break;
					case "RELATED"          :  sRELATED             = Sql.ToString (dict[sColumnName]);  break;
					case "ASSIGNED_USER_ID" :  gASSIGNED_USER_ID    = Sql.ToGuid   (dict[sColumnName]);  break;
					case "ASSIGNED_SET_LIST":  sASSIGNED_SET_LIST   = Sql.ToString (dict[sColumnName]);  break;
					case "TEAM_ID"          :  gTEAM_ID             = Sql.ToGuid   (dict[sColumnName]);  break;
					case "TEAM_SET_LIST"    :  sTEAM_SET_LIST       = Sql.ToString (dict[sColumnName]);  break;
					case "TAG_SET_NAME"     :  sTAG_SET_NAME        = Sql.ToString (dict[sColumnName]);  break;
					case "DESCRIPTION"      :  sDESCRIPTION         = Sql.ToString (dict[sColumnName]);  break;
					case "Preview"          :  bPreview             = Sql.ToBoolean(dict[sColumnName]);  break;
					case "UseTransaction"   :  bUseTransaction      = Sql.ToBoolean(dict[sColumnName]);  break;
					case "filterXml"        :  dictFilterXml        = dict[sColumnName] as Dictionary<string, object>;  break;
					case "relatedModuleXml" :  dictRelatedModuleXml = dict[sColumnName] as Dictionary<string, object>;  break;
					case "relationshipXml"  :  dictRelationshipXml  = dict[sColumnName] as Dictionary<string, object>;  break;
					case "rulesXml"         :  dictRulesXml         = dict[sColumnName] as Dictionary<string, object>;  break;
				}
			}
			// 05/16/2021 Paul.  Precheck access to filter module. 
			nACLACCESS = Security.GetUserAccess(sMODULE, "edit");
			if ( !Security.IsAuthenticated() || !Sql.ToBoolean(Application["Modules." + sMODULE + ".RestEnabled"]) || nACLACCESS < 0 )
			{
				throw(new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sMODULE));
			}
			rdl.SetCustomProperty              ("Module"        , sMODULE );
			rdl.SetCustomProperty              ("Related"       , sRELATED);
			// 06/02/2021 Paul.  React client needs to share code. 
			rdl.SetFiltersCustomProperty       (dictFilterXml       );
			rdl.SetRelatedModuleCustomProperty (dictRelatedModuleXml);
			rdl.SetRelationshipCustomProperty  (dictRelationshipXml );
			// 06/02/2021 Paul.  React client needs to share code. 
			DataTable dtRules = RulesUtil.BuildRuleDataTable(dictRulesXml);
			
			Hashtable hashAvailableModules = new Hashtable();
			StringBuilder sbErrors = new StringBuilder();
			string sReportSQL = String.Empty;
			sReportSQL = QueryBuilder.BuildReportSQL(Application, rdl, bPrimaryKeyOnly, bUseSQLParameters, bDesignChart, bUserSpecific, sMODULE, sRELATED, hashAvailableModules, sbErrors);
			if ( sbErrors.Length > 0 )
				throw(new Exception(sbErrors.ToString()));
			
			rdl.SetDataSetFields(hashAvailableModules);
			rdl.SetSingleNode("DataSets/DataSet/Query/CommandText", sReportSQL);
			
			string        sWizardModule = sMODULE;
			int           nSuccessCount = 0;
			int           nFailedCount  = 0;
			string        sStatus       = String.Empty;
			DataTable     dt            = new DataTable();
			StringBuilder sbDumpSQL     = new StringBuilder();
			// 12/12/2012 Paul.  For security reasons, we want to restrict the data types available to the rules wizard. 
			SplendidRulesTypeProvider typeProvider = new SplendidRulesTypeProvider();
			RuleValidation validation = new RuleValidation(typeof(SplendidWizardThis), typeProvider);
			RuleSet rules = RulesUtil.BuildRuleSet(dtRules, validation);
			try
			{
				DbProviderFactory dbf = DbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						string sTABLE_NAME = Modules.TableName(sWizardModule);
						cmd.CommandText = "select *" + ControlChars.CrLf
						                + "  from vw" + sTABLE_NAME + "_List" + ControlChars.CrLf;
						Security.Filter(cmd, sWizardModule, "list");
						if ( !Sql.IsEmptyString(sReportSQL) )
						{
							cmd.CommandText += "   and ID in " + ControlChars.CrLf
							                + "(" + sReportSQL + ")" + ControlChars.CrLf;
						}
						cmd.CommandText += "order by NAME asc";
						
						sbDumpSQL.Append(Sql.ClientScriptBlock(cmd));
						
						using ( DbDataAdapter da = dbf.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							da.Fill(dt);
							dt.Columns.Add("IMPORT_ROW_STATUS" , typeof(System.Boolean));
							dt.Columns.Add("IMPORT_ROW_NUMBER" , typeof(System.Int32  ));
							dt.Columns.Add("IMPORT_ROW_ERROR"  , typeof(System.String ));
							dt.Columns.Add("IMPORT_LAST_COLUMN", typeof(System.String ));
						}
					}
					if ( bPreview )
					{
						int nRowNumber = 0;
						int nFailed    = 0;
						foreach ( DataRow row in dt.Rows )
						{
							// 01/22/2015 Paul.  Move the catch inside the loop so that we can see all the errors. 
							try
							{
								row["IMPORT_ROW_NUMBER"] = nRowNumber;
								// 04/27/2018 Paul.  We need to be able to generate an error message. 
								SplendidControl Container = new SplendidControl(httpContextAccessor, Session, SplendidCache);
								SplendidWizardThis swThis = new SplendidWizardThis(Security, Container, sWizardModule, row);
								RuleExecution exec = new RuleExecution(validation, swThis);
								// 10/25/2010 Paul.  You have to be careful with Reevaluation Always as it will re-evaluate 
								// after the Then or Else actions to see if it needs to be run again. 
								// This can cause an endless loop. 
								rules.Execute(exec);
								if ( !Sql.IsEmptyString(swThis.ErrorMessage) )
									throw(new Exception(swThis.ErrorMessage));
								nRowNumber++;
								row["IMPORT_ROW_STATUS"] = true;
							}
							catch(Exception ex)
							{
								// 01/22/2015 Paul.  Save each row error. 
								row["IMPORT_ROW_ERROR" ] = ex.Message;
								row["IMPORT_ROW_STATUS"] = false;
								nFailed++;
								nSuccessCount = nRowNumber;
								nFailedCount  = nFailed   ;
							}
						}
						if ( nFailed > 0 )
							sStatus = L10n.Term("Import.LBL_FAIL");
						else
							sStatus = L10n.Term("Import.LBL_SUCCESS");
						nSuccessCount = nRowNumber;
						nFailedCount  = nFailed   ;
					}
					else
					{
						// 11/29/2010 Paul.  Make sure to check the access rights before applying the rules. 
						if ( Security.GetUserAccess(sWizardModule, "edit") >= 0 )
						{
							// 05/17/2021 Paul.  Convert SubmitRules to static function so that it can be called by React client. 
							SplendidControl Container = new SplendidControl(httpContextAccessor, Session, SplendidCache);
							EditView.SubmitRules(Session, Security, SqlProcs, Container, L10n, con, sWizardModule, rules, validation, dt, bUseTransaction, ref nSuccessCount, ref nFailedCount, ref sStatus);
						}
					}
				}
			}
			catch(Exception ex)
			{
				throw(new Exception(ex.Message + ControlChars.CrLf + RulesUtil.GetValidationErrors(validation)));
			}
			
			string sProcessedFileID   = Guid.NewGuid().ToString();
			string sProcessedFileName = Security.USER_ID.ToString() + " " + Guid.NewGuid().ToString() + ".xml";
			DataSet dsProcessed = new DataSet();
			dsProcessed.Tables.Add(dt);
			dsProcessed.WriteXml(Path.Combine(Path.GetTempPath(), sProcessedFileName), XmlWriteMode.WriteSchema);
			Session["TempFile." + sProcessedFileID] = sProcessedFileName;
			
			Dictionary<string, object> dictResponse = new Dictionary<string, object>();
			dictResponse.Add("SuccessCount"   , nSuccessCount   );
			dictResponse.Add("FailedCount"    , nFailedCount    );
			dictResponse.Add("Status"         , sStatus         );
			dictResponse.Add("ProcessedFileID", sProcessedFileID);
			if ( Sql.ToBoolean(Application["CONFIG.show_sql"]) )
			{
				dictResponse.Add("__sql", sbDumpSQL.ToString());
			}
			return dictResponse;
		}

		protected class EditView
		{
			public const int nMAX_ERRORS = 200;

			// 05/17/2021 Paul.  Convert SubmitRules to static function so that it can be called by React client. 
			public static void SubmitRules(HttpSessionState Session, Security Security, SqlProcs SqlProcs, SplendidControl Container, L10N L10n, IDbConnection con, string sMODULE_NAME, RuleSet rules, RuleValidation validation, DataTable dtData, bool bUseTransaction, ref int nSuccessCount, ref int nFailedCount, ref string sStatus)
			{
				SplendidCRM.DbProviderFactories  DbProviderFactories = new SplendidCRM.DbProviderFactories();
				HttpApplicationState             Application         = new HttpApplicationState();
				Sql                              Sql                 = new Sql(Session, Security);

				int nFailed     = 0;
				int nRowNumber  = 0;
				IDbTransaction trn = null;
				try
				{
					string sTABLE_NAME = Sql.ToString(Application["Modules." + sMODULE_NAME + ".TableName"]);
					if ( Sql.IsEmptyString(sTABLE_NAME) )
						sTABLE_NAME = sMODULE_NAME.ToUpper();
				
					IDbCommand cmdImport = null;
					cmdImport = SqlProcs.Factory(con, "sp" + sTABLE_NAME + "_Update");

					DataTable dtColumns = new DataTable();
					dtColumns.Columns.Add("ColumnName"  , Type.GetType("System.String"));
					dtColumns.Columns.Add("NAME"        , Type.GetType("System.String"));
					dtColumns.Columns.Add("DISPLAY_NAME", Type.GetType("System.String"));
					dtColumns.Columns.Add("ColumnType"  , Type.GetType("System.String"));
					dtColumns.Columns.Add("Size"        , Type.GetType("System.Int32" ));
					dtColumns.Columns.Add("Scale"       , Type.GetType("System.Int32" ));
					dtColumns.Columns.Add("Precision"   , Type.GetType("System.Int32" ));
					dtColumns.Columns.Add("colid"       , Type.GetType("System.Int32" ));
					dtColumns.Columns.Add("CustomField" , Type.GetType("System.Boolean"));
					for ( int i =0; i < cmdImport.Parameters.Count; i++ )
					{
						IDbDataParameter par = cmdImport.Parameters[i] as IDbDataParameter;
						DataRow row = dtColumns.NewRow();
						dtColumns.Rows.Add(row);
						row["ColumnName"  ] = par.ParameterName;
						row["NAME"        ] = Sql.ExtractDbName(cmdImport, par.ParameterName);
						row["DISPLAY_NAME"] = row["NAME"];
						row["ColumnType"  ] = par.DbType.ToString();
						row["Size"        ] = par.Size         ;
						row["Scale"       ] = par.Scale        ;
						row["Precision"   ] = par.Precision    ;
						row["colid"       ] = i                ;
						row["CustomField" ] = false            ;
					}
					string sSQL;
					sSQL = "select *                       " + ControlChars.CrLf
						 + "  from vwSqlColumns            " + ControlChars.CrLf
						 + " where ObjectName = @OBJECTNAME" + ControlChars.CrLf
						 + "   and ColumnName <> 'ID_C'    " + ControlChars.CrLf
						 + " order by colid                " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@OBJECTNAME", Sql.MetadataName(cmd, sTABLE_NAME + "_CSTM"));
						DbProviderFactory dbf = DbProviderFactories.GetFactory();
						using ( DbDataAdapter da = dbf.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							DataTable dtCSTM = new DataTable();
							da.Fill(dtCSTM);
							foreach ( DataRow rowCSTM in dtCSTM.Rows )
							{
								DataRow row = dtColumns.NewRow();
								row["ColumnName"  ] = Sql.ToString (rowCSTM["ColumnName"]);
								row["NAME"        ] = Sql.ToString (rowCSTM["ColumnName"]);
								row["DISPLAY_NAME"] = Sql.ToString (rowCSTM["ColumnName"]);
								row["ColumnType"  ] = Sql.ToString (rowCSTM["CsType"    ]);
								row["Size"        ] = Sql.ToInteger(rowCSTM["length"    ]);
								row["colid"       ] = dtColumns.Rows.Count;
								row["CustomField" ] = true;
								dtColumns.Rows.Add(row);
							}
						}
					}
					DataView vwColumns = new DataView(dtColumns);
					IDbCommand cmdImportCSTM = null;
					vwColumns.RowFilter = "CustomField = 1";
					if ( vwColumns.Count > 0 )
					{
						vwColumns.Sort = "colid";
						cmdImportCSTM = con.CreateCommand();
						cmdImportCSTM.CommandType = CommandType.Text;
						cmdImportCSTM.CommandText = "update " + sTABLE_NAME + "_CSTM" + ControlChars.CrLf;
						int nFieldIndex = 0;
						foreach ( DataRowView row in vwColumns )
						{
							string sNAME   = Sql.ToString(row["ColumnName"]).ToUpper();
							string sCsType = Sql.ToString(row["ColumnType"]);
							int    nMAX_SIZE = Sql.ToInteger(row["Size"]);
							if ( nFieldIndex == 0 )
								cmdImportCSTM.CommandText += "   set ";
							else
								cmdImportCSTM.CommandText += "     , ";
							cmdImportCSTM.CommandText += sNAME + " = @" + sNAME + ControlChars.CrLf;
						
							IDbDataParameter par = null;
							switch ( sCsType )
							{
								case "Guid"    :  par = Sql.AddParameter(cmdImportCSTM, "@" + sNAME, Guid.Empty             );  break;
								case "short"   :  par = Sql.AddParameter(cmdImportCSTM, "@" + sNAME, 0                      );  break;
								case "Int32"   :  par = Sql.AddParameter(cmdImportCSTM, "@" + sNAME, 0                      );  break;
								case "Int64"   :  par = Sql.AddParameter(cmdImportCSTM, "@" + sNAME, 0                      );  break;
								case "float"   :  par = Sql.AddParameter(cmdImportCSTM, "@" + sNAME, 0.0f                   );  break;
								case "decimal" :  par = Sql.AddParameter(cmdImportCSTM, "@" + sNAME, new Decimal()          );  break;
								case "bool"    :  par = Sql.AddParameter(cmdImportCSTM, "@" + sNAME, false                  );  break;
								case "DateTime":  par = Sql.AddParameter(cmdImportCSTM, "@" + sNAME, DateTime.MinValue      );  break;
								default        :  par = Sql.AddParameter(cmdImportCSTM, "@" + sNAME, String.Empty, nMAX_SIZE);  break;
							}
							nFieldIndex++;
						}
						cmdImportCSTM.CommandText += " where ID_C = @ID_C" + ControlChars.CrLf;
						Sql.AddParameter(cmdImportCSTM, "@ID_C", Guid.Empty);
						// 01/16/2011 Paul.  Not sure what this execute was for.  Seems to be a cut-and-paste bug. 
						//cmdImportCSTM.ExecuteNonQuery();
					}
					vwColumns.RowFilter = "";
					if ( bUseTransaction )
					{
						trn = Sql.BeginTransaction(con);
						cmdImport.Transaction = trn;
						if ( cmdImportCSTM != null )
							cmdImportCSTM.Transaction = trn;
					}
				
					foreach ( DataRow row in dtData.Rows )
					{
						// 04/27/2018 Paul.  We need to be able to generate an error message. 
						SplendidWizardThis swThis = new SplendidWizardThis(Security, Container, sMODULE_NAME, row);
						RuleExecution exec = new RuleExecution(validation, swThis);
						rules.Execute(exec);
						if ( !Sql.IsEmptyString(swThis.ErrorMessage) )
							throw(new Exception(swThis.ErrorMessage));
						nRowNumber++;
						row["IMPORT_ROW_NUMBER"] = nRowNumber ;
					
						Guid gID = Sql.ToGuid(row["ID"]);
						try
						{
							foreach(IDbDataParameter par in cmdImport.Parameters)
							{
								par.Value = DBNull.Value;
							}
							if ( cmdImportCSTM != null )
							{
								foreach(IDbDataParameter par in cmdImportCSTM.Parameters)
								{
									par.Value = DBNull.Value;
								}
							}
							Sql.SetParameter(cmdImport, "@MODIFIED_USER_ID", Security.USER_ID);
							foreach ( DataColumn col in dtData.Columns )
							{
								string sName = col.ColumnName;
								if (  sName == "IMPORT_ROW_STATUS"  
								   || sName == "IMPORT_ROW_NUMBER"  
								   || sName == "IMPORT_ROW_ERROR"   
								   || sName == "IMPORT_LAST_COLUMN" 
								   )
									continue;
								row["IMPORT_ROW_STATUS" ] = true ;
								row["IMPORT_LAST_COLUMN"] = sName;
								IDbDataParameter par = Sql.FindParameter(cmdImport, sName);
								if ( par != null )
								{
									par.Value = row[col.ColumnName];
								}
								else if ( cmdImportCSTM != null )
								{
									par = Sql.FindParameter(cmdImportCSTM, sName);
									if ( par != null )
									{
										par.Value = row[col.ColumnName];
									}
								}
							}
							cmdImport.ExecuteNonQuery();
							if ( cmdImportCSTM != null )
							{
								Sql.SetParameter(cmdImportCSTM, "ID_C", gID);
								cmdImportCSTM.ExecuteNonQuery();
							}
							row["IMPORT_LAST_COLUMN"] = DBNull.Value;
						}
						catch(Exception ex)
						{
							row["IMPORT_ROW_STATUS"] = false;
							row["IMPORT_ROW_ERROR" ] = L10n.Term("RulesWizard.LBL_ERROR") + " " + Sql.ToString(row["IMPORT_LAST_COLUMN"]) + ". " + ex.Message;
							nFailed++;
							// 10/31/2006 Paul.  Abort after 200 errors. 
							if ( nFailed >= nMAX_ERRORS )
							{
								//ctlDynamicButtons.ErrorText += L10n.Term("RulesWizard.LBL_MAX_ERRORS");
								break;
							}
						}
					}
					if ( trn != null )
					{
						trn.Commit();
					}
				}
				finally
				{
					if ( trn != null )
						trn.Dispose();
				}
				if ( nFailed == 0 )
					sStatus = L10n.Term("Import.LBL_SUCCESS");
				else if ( nFailed >= nMAX_ERRORS )
					sStatus = L10n.Term("RulesWizard.LBL_MAX_ERRORS");
				else
					sStatus = L10n.Term("Import.LBL_FAIL");
				nSuccessCount = nRowNumber;
				nFailedCount  = nFailed   ;
			}
		}
	}
}
