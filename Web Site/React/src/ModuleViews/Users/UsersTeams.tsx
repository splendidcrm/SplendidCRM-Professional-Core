/*
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *
 * Any use of the contents of this file are subject to the SplendidCRM Professional Source Code License 
 * Agreement, or other written agreement between you and SplendidCRM ("License"). By installing or 
 * using this file, you have unconditionally agreed to the terms and conditions of the License, 
 * including but not limited to restrictions on the number of users therein, and you may not use this 
 * file except in compliance with the License. 
 * 
 */

// 1. React and fabric. 
import * as React from 'react';
import posed                                  from 'react-pose'                       ;
import { RouteComponentProps, withRouter }    from 'react-router-dom'                 ;
import { observer }                           from 'mobx-react'                       ;
import { FontAwesomeIcon }                    from '@fortawesome/react-fontawesome'   ;
import { Appear }                             from 'react-lifecycle-appear'           ;
// 2. Store and Types. 
import DETAILVIEWS_RELATIONSHIP               from '../../types/DETAILVIEWS_RELATIONSHIP';
import RELATIONSHIPS                          from '../../types/RELATIONSHIPS'           ;
import { SubPanelHeaderButtons }              from '../../types/SubPanelHeaderButtons'   ;
// 3. Scripts. 
import Sql                                    from '../../scripts/Sql'                   ;
import L10n                                   from '../../scripts/L10n'                  ;
import Credentials                            from '../../scripts/Credentials'           ;
import SplendidCache                          from '../../scripts/SplendidCache'         ;
import { DynamicLayout_Module }               from '../../scripts/DynamicLayout'         ;
import { ListView_LoadTablePaginated }        from '../../scripts/ListView'              ;
import { AuthenticatedMethod, LoginRedirect } from '../../scripts/Login'                 ;
import { Crm_Config, Crm_Modules }            from '../../scripts/Crm'                   ;
import { EndsWith }                           from '../../scripts/utility'               ;
import { UpdateModule, UpdateRelatedItem, UpdateRelatedList, DeleteRelatedItem } from '../../scripts/ModuleUpdate';
// 4. Components and Views. 
import ErrorComponent                         from '../../components/ErrorComponent'    ;
import SplendidGrid                           from '../../components/SplendidGrid'      ;
import DynamicButtons                         from '../../components/DynamicButtons'    ;
import SearchView                             from '../../views/SearchView'             ;
import PopupView                              from '../../views/PopupView'              ;
import EditView                               from '../../views/EditView'               ;
import SubPanelButtonsFactory                 from '../../ThemeComponents/SubPanelButtonsFactory';

const Content = posed.div(
{
	open:
	{
		height: '100%'
	},
	closed:
	{
		height: 0
	}
});

interface ISubPanelViewProps extends RouteComponentProps<any>
{
	PARENT_TYPE      : string;
	row              : any;
	layout           : DETAILVIEWS_RELATIONSHIP;
	CONTROL_VIEW_NAME: string;
	disableView?     : boolean;
	disableEdit?     : boolean;
	disableRemove?   : boolean;
	// 04/10/2021 Paul.  Create framework to allow pre-compile of all modules. 
	isPrecompile?       : boolean;
	onComponentComplete?: (MODULE_NAME, RELATED_MODULE, LAYOUT_NAME, data) => void;
}

interface ISubPanelViewState
{
	PARENT_ID        : string;
	RELATED_MODULE?  : string;
	GRID_NAME?       : string;
	TABLE_NAME?      : string;
	SORT_FIELD?      : string;
	SORT_DIRECTION?  : string;
	PRIMARY_FIELD?   : string;
	PRIMARY_ID?      : string;
	showCancel       : boolean;
	showFullForm     : boolean;
	showTopButtons   : boolean;
	showBottomButtons: boolean;
	showSearch       : boolean;
	showInlineEdit   : boolean;
	multiSelect      : boolean;
	popupOpen        : boolean;
	archiveView      : boolean;
	item?            : any;
	rowInitialValues : any;
	dependents?      : Record<string, Array<any>>;
	error?           : any;
	open             : boolean;
	customView       : any;
	isTeamsDisabled  : boolean;
	subPanelVisible  : boolean;
}

@observer
class UsersTeams extends React.Component<ISubPanelViewProps, ISubPanelViewState>
{
	private _isMounted = false;

	private searchView           = React.createRef<SearchView>();
	private splendidGrid         = React.createRef<SplendidGrid>();
	private dynamicButtonsTop    = React.createRef<DynamicButtons>();
	private dynamicButtonsBottom = React.createRef<DynamicButtons>();
	private editView             = React.createRef<EditView>();
	private headerButtons        = React.createRef<SubPanelHeaderButtons>();

	constructor(props: ISubPanelViewProps)
	{
		super(props);
		//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '.constructor ' + props.PARENT_TYPE, props.layout);
		let archiveView: boolean = false;
		let GRID_NAME  : string = props.PARENT_TYPE + '.' + props.layout.CONTROL_NAME;
		if ( props.location.pathname.indexOf('/ArchiveView') >= 0 )
		{
			archiveView = true;
			// 01/27/2020 Paul.  Contacts.Activities.History.ArchiveView is not correct.  We simplified to Contacts.Activities.ArchiveView. 
			if ( props.layout.CONTROL_NAME == 'Activities.History' )
			{
				GRID_NAME = props.PARENT_TYPE + '.Activities.ArchiveView';
			}
			else
			{
				GRID_NAME  += '.ArchiveView';
			}
		}
		let rowPARENT: any = props.row;
		let rowInitialValues: any = {};
		// 04/14/2016 Paul.  We need a way to detect that we are loading EditView from a relationship create. 
		rowInitialValues['DetailViewRelationshipCreate'] = true;
		rowInitialValues['PARENT_ID'  ] = rowPARENT.ID  ;
		rowInitialValues['PARENT_NAME'] = rowPARENT.NAME;
		// 01/30/2013 Paul.  Include the parent type to make sure that the dropdown is set properly for an activity record. 
		rowInitialValues['PARENT_TYPE'] = props.PARENT_TYPE;

		// 04/18/2021 Paul.  Hard code values as Teams module may not be enabled. 
		let PARENT_TABLE      = 'USERS';
		let PARENT_ID_FIELD   = 'USER_ID'  ;
		let PARENT_NAME_FIELD = 'USER_NAME';
		rowInitialValues[PARENT_ID_FIELD  ] = rowPARENT.ID  ;
		rowInitialValues[PARENT_NAME_FIELD] = rowPARENT.NAME;
		
		// 07/01/2019 Paul.  The SubPanelsView needs to understand how to manage all relationships. 
		// 07/02/2019 Paul.  The following is an alternative to the initial approach of setting PARENT_ID and setting table versions of the parent. 
		let multiSelect: boolean = false;
		let relationship: RELATIONSHIPS = SplendidCache.Relationship(props.PARENT_TYPE, props.layout.MODULE_NAME);
		if ( relationship != null )
		{
			if ( props.PARENT_TYPE == relationship.LHS_MODULE )
			{
				if ( relationship.RELATIONSHIP_TYPE == 'many-to-many' || relationship.RELATIONSHIP_TYPE == 'one-to-many' )
				{
					multiSelect = true;
				}
				if ( Sql.IsEmptyString(relationship.JOIN_TABLE) )
					PARENT_ID_FIELD = relationship.RHS_KEY;
				else
					PARENT_ID_FIELD = relationship.JOIN_KEY_LHS;
			}
			else
			{
				if ( relationship.RELATIONSHIP_TYPE == 'many-to-many' )
				{
					multiSelect = true;
				}
				if ( Sql.IsEmptyString(relationship.JOIN_TABLE) )
					PARENT_ID_FIELD = relationship.LHS_KEY;
				else
					PARENT_ID_FIELD = relationship.JOIN_KEY_RHS;
			}
			// 08/24/2021 Paul.  _ID is 3 characters. 
			PARENT_NAME_FIELD = PARENT_ID_FIELD.substring(0, PARENT_ID_FIELD.length - 3) + '_NAME';
		}
		rowInitialValues[PARENT_ID_FIELD  ] = rowPARENT.ID  ;
		rowInitialValues[PARENT_NAME_FIELD] = rowPARENT.NAME;

		// 04/14/2016 Paul.  New spPARENT_GetWithTeam procedure so that we can inherit Assigned To and Team values. 
		// 04/14/2016 Paul.  In order to inherit assigned user and team, might as well send the entire row. 
		if ( rowPARENT['ASSIGNED_USER_ID'] !== undefined )
		{
			rowInitialValues['ASSIGNED_USER_ID' ] = rowPARENT['ASSIGNED_USER_ID' ];
			rowInitialValues['ASSIGNED_TO'      ] = rowPARENT['ASSIGNED_TO'      ];
			rowInitialValues['ASSIGNED_TO_NAME' ] = rowPARENT['ASSIGNED_TO_NAME' ];
			// 07/02/2019 Paul.  Copy dynamic user values. 
			rowInitialValues['ASSIGNED_SET_ID'  ] = rowPARENT['ASSIGNED_SET_ID'  ];
			rowInitialValues['ASSIGNED_SET_LIST'] = rowPARENT['ASSIGNED_SET_LIST'];
			rowInitialValues['ASSIGNED_SET_NAME'] = rowPARENT['ASSIGNED_SET_NAME'];
		}
		if ( rowPARENT['TEAM_ID'] !== undefined )
		{
			rowInitialValues['TEAM_ID'      ] = rowPARENT['TEAM_ID'      ];
			rowInitialValues['TEAM_NAME'    ] = rowPARENT['TEAM_NAME'    ];
			rowInitialValues['TEAM_SET_ID'  ] = rowPARENT['TEAM_SET_ID'  ];
			rowInitialValues['TEAM_SET_LIST'] = rowPARENT['TEAM_SET_LIST'];
			rowInitialValues['TEAM_SET_NAME'] = rowPARENT['TEAM_SET_NAME'];
		}
		//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '.constructor rowInitialValues', rowInitialValues);
		//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '.constructor multiSelect', multiSelect);
		// 11/10/2020 Paul.  A customer wants to be able to have subpanels default to open. 
		let rawOpen        : string  = localStorage.getItem(props.CONTROL_VIEW_NAME);
		// 04/10/2021 Paul.  Create framework to allow pre-compile of all modules. 
		let open           : boolean = (rawOpen == 'true' || this.props.isPrecompile);
		if ( rawOpen == null && Crm_Config.ToBoolean('default_subpanel_open') )
		{
			open = true;
		}
		// 11/05/2020 Paul.  Copy initial values so that we can reuse. 
		let item: any = Object.assign({}, rowInitialValues);
		this.state =
		{
			PARENT_ID        : props.row.ID,
			RELATED_MODULE   : props.layout.MODULE_NAME,
			GRID_NAME        ,
			TABLE_NAME       : props.layout.TABLE_NAME,
			SORT_FIELD       : props.layout.SORT_FIELD,
			SORT_DIRECTION   : props.layout.SORT_DIRECTION,
			PRIMARY_FIELD    : props.layout.PRIMARY_FIELD,
			PRIMARY_ID       : props.row.ID,
			showCancel       : true,
			showFullForm     : true,
			showTopButtons   : true,
			showBottomButtons: true,
			showSearch       : false,
			showInlineEdit   : false,
			multiSelect      ,
			popupOpen        : false,
			archiveView      ,
			item             ,
			rowInitialValues ,
			dependents       : {},
			error            : null,
			open             ,
			customView       : null,
			isTeamsDisabled  : false,
			subPanelVisible  : Sql.ToBoolean(props.isPrecompile),  // 08/31/2021 Paul.  Must show sub panel during precompile to allow it to continue. 
		};
	}

	async componentDidMount()
	{
		const { RELATED_MODULE } = this.state;
		this._isMounted = true;
		try
		{
			let status = await AuthenticatedMethod(this.props, this.constructor.name + '.componentDidMount');
			if ( status == 1 )
			{
				// 01/23/2021 Paul.  Don't change the Admin mode as this panel can be used by both admins and non-admins. 
				//if ( Credentials.ADMIN_MODE )
				//{
				//	Credentials.SetADMIN_MODE(false);
				//}
				let teams = SplendidCache.Module('Teams', this.constructor.name + '.componentDidMount');
				let isTeamsDisabled: boolean = (teams == null);
				this.setState({ isTeamsDisabled });
			}
			else
			{
				LoginRedirect(this.props.history, this.constructor.name + '.componentDidMount');
			}
		}
		catch(error)
		{
			console.error((new Date()).toISOString() + ' ' + this.constructor.name + '.componentDidMount', error);
			this.setState({ error });
		}
	}

	componentWillUnmount()
	{
		this._isMounted = false;
	}

	componentDidCatch(error, info)
	{
		console.error((new Date()).toISOString() + ' ' + this.constructor.name + '.componentDidCatch', error, info);
	}

	async componentDidUpdate(prevProps: ISubPanelViewProps)
	{
		// 04/10/2021 Paul.  Create framework to allow pre-compile of all modules. 
		if ( this.props.onComponentComplete )
		{
			const { PARENT_TYPE, CONTROL_VIEW_NAME } = this.props;
			const { RELATED_MODULE, error, isTeamsDisabled } = this.state;
			//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '._onComponentComplete ' + DETAIL_NAME, item);
			if ( error == null )
			{
				// 04/18/2021 Paul.  If Teams module is disabled, there will not be a SplendidGrid completed event. 
				if ( isTeamsDisabled )
				{
					this.props.onComponentComplete(PARENT_TYPE, RELATED_MODULE, CONTROL_VIEW_NAME, null);
				}
			}
		}
	}

	// 04/10/2021 Paul.  Create framework to allow pre-compile of all modules. 
	private _onComponentComplete = (MODULE_NAME, RELATED_MODULE, LAYOUT_NAME, data): void => 
	{
		const { CONTROL_VIEW_NAME } = this.props;
		const { error } = this.state;
		//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '._onComponentComplete ' + LAYOUT_NAME, data);
		if ( this.props.onComponentComplete )
		{
			if ( error == null )
			{
				this.props.onComponentComplete(MODULE_NAME, RELATED_MODULE, CONTROL_VIEW_NAME, data);
			}
		}
	}

	// 09/26/2020 Paul.  The SearchView needs to be able to specify a sort criteria. 
	private _onSearchViewCallback = (sFILTER: string, row: any, oSORT?: any) =>
	{
		//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '._onSearchViewCallback');
		// 07/13/2019 Paul.  Make Search public so that it can be called from a refrence. 
		if ( this.splendidGrid.current != null )
		{
			this.splendidGrid.current.Search(sFILTER, row, oSORT);
		}
	}

	private _onGridLayoutLoaded = () =>
	{
		//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '._onGridLayoutLoaded');
		// 05/08/2019 Paul.  Once we have the Search callback, we can tell the SearchView to submit and it will get to the GridView. 
		// 07/13/2019 Paul.  Call SubmitSearch directly. 
		if ( this.searchView.current != null )
		{
			this.searchView.current.SubmitSearch();
		}
	}

	private Page_Command = async (sCommandName, sCommandArguments) =>
	{
		const { showSearch, showInlineEdit } = this.state;
		const { PARENT_ID, RELATED_MODULE } = this.state;
		//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '.Page_Command ' + sCommandName, sCommandArguments);
		try
		{
			if ( this._isMounted )
			{
				if ( sCommandName == 'Create' || EndsWith(sCommandName, '.Create') )
				{
					let RELATED_MODULE: string = null;
					if ( sCommandName.indexOf('.') >= 0 )
					{
						RELATED_MODULE = sCommandName.split('.')[0];
					}
					let customView = await DynamicLayout_Module(RELATED_MODULE, 'EditViews', 'EditView.Inline');
					if ( customView )
					{
						//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '.Page_Command found ' + RELATED_MODULE + '.EditView.Inline');
					}
					this.setState({ showSearch: false, showInlineEdit: true, RELATED_MODULE, customView });
				}
				else if ( sCommandName == 'Select' || EndsWith(sCommandName, '.Select') )
				{
					//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '.Page_Command show Select');
					this.setState({ popupOpen: true });
				}
				// 10/15/2020 Paul.  There are currently 5 multi-selects, Regions, Roles, Teams, Users and ZipCodes. 
				else if ( sCommandName == 'MultiSelect' || EndsWith(sCommandName, 'MultiSelect();') )
				{
					//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '.Page_Command show Select');
					this.setState({ popupOpen: true, multiSelect: true });
				}
				// 04/20/2020 Paul.  SearchOpen and SearchHistory are on the Activities panels. 
				else if ( sCommandName == 'Search' || EndsWith(sCommandName, '.Search') || EndsWith(sCommandName, '.SearchOpen') || EndsWith(sCommandName, '.SearchHistory') )
				{
					this.setState({ showSearch: !showSearch, showInlineEdit: false });
				}
				else if ( sCommandName == 'NewRecord' )
				{
					await this.Save();
				}
				else if ( sCommandName == 'NewRecord.Cancel' )
				{
					this.setState({ showInlineEdit: false, customView: null });
				}
				else if ( sCommandName == 'NewRecord.FullForm' )
				{
					// 06/28/2019 Paul.  Reset to new edit view with parent ID. 
					// 10/12/2019 Paul.  Support Full Form. 
					this.props.history.push(`/Reset/${RELATED_MODULE}/Edit?PARENT_ID=${PARENT_ID}`);
				}
				else
				{
					console.error((new Date()).toISOString() + ' ' + this.constructor.name + '.Page_Command: Unknown command ' + sCommandName);
				}
			}
		}
		catch(error)
		{
			console.error((new Date()).toISOString() + ' ' + this.constructor.name + '.Page_Command ' + sCommandName, error);
			this.setState({ error });
		}
	}

	private Save = async () =>
	{
		const { PARENT_TYPE } = this.props;
		const { PARENT_ID, RELATED_MODULE } = this.state;
		try
		{
			if ( this.editView.current != null && this.editView.current.validate() )
			{
				let row: any = this.editView.current.data;
				//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '.Save ' + PARENT_TYPE, row);
				try
				{
					// 01/08/2020 Paul.  Disable buttons before update. 
					if ( this.dynamicButtonsTop.current != null )
					{
						this.dynamicButtonsTop.current.EnableButton('NewRecord', false);
						// 06/03/2021 Paul.  Show and hide busy while saving new record. 
						this.dynamicButtonsTop.current.Busy();
					}
					if ( this.dynamicButtonsBottom.current != null )
					{
						this.dynamicButtonsBottom.current.EnableButton('NewRecord', false);
					}
					let sID = await UpdateModule(RELATED_MODULE, row, null);
					// 04/20/2020 Paul.  Notes is special in that it uses the parent to establish the relationship. 
					if ( RELATED_MODULE != 'Notes' )
					{
						let sPRIMARY_MODULE = PARENT_TYPE   ;
						let sPRIMARY_ID     = PARENT_ID     ;
						let sRELATED_MODULE = RELATED_MODULE;
						let sRELATED_ID     = sID           ;
						await UpdateRelatedItem(sPRIMARY_MODULE, sPRIMARY_ID, sRELATED_MODULE, sRELATED_ID);
					}
					if ( this._isMounted )
					{
						// 07/18/2019 Paul.  We also need to clear the input fields. 
						if ( this.editView.current != null )
						{
							this.editView.current.clear();
						}
						// 07/13/2019 Paul.  Call SubmitSearch directly. 
						if ( this.searchView.current != null )
						{
							this.searchView.current.SubmitSearch();
						}
						// 03/17/2020 Paul.  Set the state after clearing the form, otherwise this.editView.current will be null. 
						// 03/17/2020 Paul.  Clear the local item as well. 
						// 11/05/2020 Paul.  Copy initial values so that we can reuse. 
						let item: any = Object.assign({}, this.state.rowInitialValues);
						this.setState({ showInlineEdit: false, item });
					}
				}
				catch(error)
				{
					console.error((new Date()).toISOString() + ' ' + this.constructor.name + '.Save', error);
					if ( error.message.includes('.ERR_DUPLICATE_EXCEPTION') )
					{
						if ( this.dynamicButtonsTop.current != null )
						{
							this.dynamicButtonsTop.current.ShowButton('SaveDuplicate', true);
						}
						if ( this.dynamicButtonsBottom.current != null )
						{
							this.dynamicButtonsBottom.current.ShowButton('SaveDuplicate', true);
						}
						this.setState( {error: L10n.Term(error.message) } );
					}
					else
					{
						this.setState({ error });
					}
				}
				finally
				{
					if ( this.dynamicButtonsTop.current != null )
					{
						this.dynamicButtonsTop.current.EnableButton('NewRecord', true);
						// 06/03/2021 Paul.  Show and hide busy while saving new record. 
						this.dynamicButtonsTop.current.NotBusy();
					}
					if ( this.dynamicButtonsBottom.current != null )
					{
						this.dynamicButtonsBottom.current.EnableButton('NewRecord', true);
					}
				}
			}
		}
		catch(error)
		{
			console.error((new Date()).toISOString() + ' ' + this.constructor.name + '.Save', error);
			this.setState({ error });
		}
	}

	private editViewCallback = (key, newValue) =>
	{
		//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '.editViewCallback ' + DATA_FIELD, DATA_VALUE);
		let item = this.state.item;
		if ( item == null )
			item = {};
		item[key] = newValue;
		if ( this._isMounted )
		{
			this.setState({ item });
		}
	}

	private _onSelect = async (value: { Action: string, ID: string, NAME: string, selectedItems: any }) =>
	{
		const { PARENT_TYPE } = this.props;
		const { PARENT_ID, RELATED_MODULE } = this.state;
		//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '._onSelect ' + PARENT_TYPE, value);
		if ( value.Action == 'SingleSelect' )
		{
			try
			{
				let sPRIMARY_MODULE = PARENT_TYPE   ;
				let sPRIMARY_ID     = PARENT_ID     ;
				let sRELATED_MODULE = RELATED_MODULE;
				let sRELATED_ID     = value.ID      ;
				await UpdateRelatedItem(sPRIMARY_MODULE, sPRIMARY_ID, sRELATED_MODULE, sRELATED_ID);
				if ( this._isMounted )
				{
					this.setState({ popupOpen: false }, () =>
					{
						// 07/13/2019 Paul.  Call SubmitSearch directly. 
						if ( this.searchView.current != null )
						{
							this.searchView.current.SubmitSearch();
						}
					});
				}
			}
			catch(error)
			{
				console.error((new Date()).toISOString() + ' ' + this.constructor.name + '._onSelect', error);
				// 07/07/2020 Paul.  Make sure to close the popup on error. 
				this.setState({ error, popupOpen: false });
			}
		}
		else if ( value.Action == 'MultipleSelect' )
		{
			try
			{
				let sPRIMARY_MODULE = PARENT_TYPE   ;
				let sPRIMARY_ID     = PARENT_ID     ;
				let sRELATED_MODULE = RELATED_MODULE;
				let arrRELATED_ID   = [];
				for ( let sRELATED_ID in value.selectedItems )
				{
					arrRELATED_ID.push(sRELATED_ID);
				}
				// 07/09/2019 Paul.  UpdateRelatedList is identical to UpdateRelatedItem but accepts an array. 
				await UpdateRelatedList(sPRIMARY_MODULE, sPRIMARY_ID, sRELATED_MODULE, arrRELATED_ID);
				if ( this._isMounted )
				{
					this.setState({ popupOpen: false }, () =>
					{
						// 07/13/2019 Paul.  Call SubmitSearch directly. 
						if ( this.searchView.current != null )
						{
							this.searchView.current.SubmitSearch();
						}
					});
				}
			}
			catch(error)
			{
				console.error((new Date()).toISOString() + ' ' + this.constructor.name + '._onSelect', error);
				// 07/07/2020 Paul.  Make sure to close the popup on error. 
				this.setState({ error, popupOpen: false });
			}
		}
		else if ( value.Action == 'Close' )
		{
			this.setState({ popupOpen: false });
		}
	}

	private _onRemove = async (row) =>
	{
		const { PARENT_TYPE } = this.props;
		const { PARENT_ID, RELATED_MODULE } = this.state;
		//console.log((new Date()).toISOString() + ' ' + this.constructor.name + '._onRemove ' + PARENT_TYPE, row);
		try
		{
			// 10/12/2020 Paul.  Confirm remove. 
			if ( window.confirm(L10n.Term('.NTC_REMOVE_CONFIRMATION')) )
			{
				let sPRIMARY_MODULE = PARENT_TYPE   ;
				let sPRIMARY_ID     = PARENT_ID     ;
				let sRELATED_MODULE = RELATED_MODULE;
				let sRELATED_ID     = row.ID        ;
				await DeleteRelatedItem(sPRIMARY_MODULE, sPRIMARY_ID, sRELATED_MODULE, sRELATED_ID);
				if ( this._isMounted )
				{
					this.setState({ popupOpen: false }, () =>
					{
						// 07/13/2019 Paul.  Call SubmitSearch directly. 
						if ( this.searchView.current != null )
						{
							this.searchView.current.SubmitSearch();
						}
					});
				}
			}
		}
		catch(error)
		{
			console.error((new Date()).toISOString() + ' ' + this.constructor.name + '._onRemove', error);
			this.setState({ error });
		}
	}

	private onToggleCollapse = (open) =>
	{
		const { CONTROL_VIEW_NAME } = this.props;
		this.setState({ open }, () =>
		{
			if ( open )
			{
				localStorage.setItem(CONTROL_VIEW_NAME, 'true');
			}
			else
			{
				// 11/10/2020 Paul.  Save false instead of remove so that config value default_subpanel_open will work properly. 
				//localStorage.removeItem(CONTROL_VIEW_NAME);
				localStorage.setItem(CONTROL_VIEW_NAME, 'false');
			}
		});
	}

	private _onButtonsLoaded = async () =>
	{
		if ( this.headerButtons.current != null )
		{
			// 11/19/2008 Paul.  HideAll must be after the buttons are appended.
			if ( SplendidCache.AdminUserAccess('Teams', 'edit') < 0 )
			{
				this.headerButtons.current.HideAll();
			}
		}
	}

	private Load = async (sMODULE_NAME, sSORT_FIELD, sSORT_DIRECTION, sSELECT, sFILTER, rowSEARCH_VALUES, nTOP, nSKIP, bADMIN_MODE?, archiveView?) =>
	{
		const { TABLE_NAME, PRIMARY_FIELD, PRIMARY_ID } = this.state;
		// 05/28/2020 Paul.  The TEAM_ID field is not normally returned as part of the layout, so we have to add manually. 
		let arrSELECT: string[] = sSELECT.split(',');
		if ( arrSELECT.indexOf('TEAM_ID') < 0 )
		{
			arrSELECT.push('TEAM_ID');
		}
		sSELECT = arrSELECT.join(',');
		// 05/28/2020 Paul.  We also need to manually add the relationship as we skip this code in SplendidGrid. 
		rowSEARCH_VALUES = Sql.DeepCopy(rowSEARCH_VALUES);
		if ( rowSEARCH_VALUES == null )
		{
			rowSEARCH_VALUES = {};
		}
		rowSEARCH_VALUES[PRIMARY_FIELD] = { FIELD_TYPE: 'Hidden', value: PRIMARY_ID };

		let d = await ListView_LoadTablePaginated(TABLE_NAME, sSORT_FIELD, sSORT_DIRECTION, sSELECT, sFILTER, rowSEARCH_VALUES, nTOP, nSKIP, bADMIN_MODE, archiveView);
		if ( d.results )
		{
			for ( let i: number = 0; i < d.results.length; i++ )
			{
				// 05/28/2020 Paul.  The SplendidGrid needs an ID fields to enable the Remove link. 
				d.results[i]['ID'] = L10n.Term(d.results[i]['TEAM_ID']);
			}
		}
		return d;
	}

	public render()
	{
		const { PARENT_TYPE, row, layout, CONTROL_VIEW_NAME, disableView, disableEdit, disableRemove } = this.props;
		const { RELATED_MODULE, GRID_NAME, TABLE_NAME, SORT_FIELD, SORT_DIRECTION, PRIMARY_FIELD, PRIMARY_ID, error, showCancel, showFullForm, showTopButtons, showBottomButtons, showSearch, showInlineEdit, item, popupOpen, multiSelect, archiveView, open, customView, isTeamsDisabled, subPanelVisible } = this.state;
		// 05/04/2019 Paul.  Reference obserable IsInitialized so that terminology update will cause refresh. 
		// 05/06/2019 Paul.  The trick to having the SearchView change with the tabs is to change the key. 
		// 06/25/2019 Paul.  The SplendidGrid is getting a componentDidUpdate event instead of componentDidMount, so try specifying a key. 
		let sNewRecordButtons: string = "NewRecord." + (showFullForm ? "FullForm" : (showCancel ? "WithCancel" : "SaveOnly"));
		// 06/29/2019 Paul.  The search panel must always be rendered so that it can fire the first search event to the ListView. 
		let cssSearch = { display: (showSearch ? 'inline' : 'none') };
		let cbRemove = (multiSelect ? this._onRemove : null);
		// 04/18/2021 Paul.  Teams may be disabled or this may be the Community edition. 
		if ( isTeamsDisabled )
		{
			return null;
		}
		else if ( SplendidCache.IsInitialized  )
		{
			// 12/04/2019 Paul.  After authentication, we need to make sure that the app gets updated. 
			Credentials.sUSER_THEME;
			let headerButtons = SubPanelButtonsFactory(SplendidCache.UserTheme);
			let MODULE_NAME      : string = RELATED_MODULE;
			let MODULE_TITLE     : string = L10n.Term(layout.TITLE);
			let EDIT_NAME        : string = MODULE_NAME + '.SearchSubpanel';
			// 04/20/2020 Paul.  ActivitiesOpen and ActivitiesHistory need to have a different EDIT_NAME as it is used in the ID. 
			// Most relationship panels have both Open and History, so we need to ensure that unqiue IDs are generated. 
			if ( EDIT_NAME == 'Activities.SearchSubpanel' )
			{
				if ( this.props.CONTROL_VIEW_NAME.indexOf('ActivitiesHistory') > 0 )
				{
					EDIT_NAME = 'Activities.SearchSubpanelHistory';
				}
			}
			let ACLACCESS_Edit: boolean = SplendidCache.AdminUserAccess('Users', 'edit') >= 0;
			// 07/30/2021 Paul.  Load when the panel appears. 
			return (
				<React.Fragment>
					<PopupView
						isOpen={ popupOpen }
						callback={ this._onSelect }
						MODULE_NAME={ MODULE_NAME }
						multiSelect={ multiSelect }
						ClearDisabled={ true }
					/>
					<Appear onAppearOnce={ (ioe) => this.setState({ subPanelVisible: true }) }>
						{ headerButtons
						? React.createElement(headerButtons, { MODULE_NAME, ID: null, MODULE_TITLE, CONTROL_VIEW_NAME, error, ButtonStyle: 'ListHeader', VIEW_NAME: GRID_NAME, row: item, Page_Command: this.Page_Command, showButtons: !showInlineEdit, onToggle: this.onToggleCollapse, isPrecompile: this.props.isPrecompile, onLayoutLoaded: this._onButtonsLoaded, history: this.props.history, location: this.props.location, match: this.props.match, ref: this.headerButtons })
						: null
						}
					</Appear>
					<Content pose={ open ? 'open' : 'closed' } style={ {overflow: (open ? 'visible' : 'hidden')} }>
						{ open && subPanelVisible
						? <React.Fragment>
							<div style={ cssSearch }>
								<div className="card" style={{marginBottom: '0.5rem'}}>
									<div className="card-body">
										<SearchView
											key={ EDIT_NAME }
											EDIT_NAME={ EDIT_NAME }
											AutoSaveSearch={ false }
											ShowSearchViews={ false }
											cbSearch={ this._onSearchViewCallback }
											history={ this.props.history }
											location={ this.props.location }
											match={ this.props.match }
											ref={ this.searchView }
										/>
									</div>
								</div> 
							</div>
							{ showInlineEdit
							? <div>
								{ showTopButtons
								? <div>
									<DynamicButtons
										ButtonStyle="EditHeader"
										VIEW_NAME={ sNewRecordButtons }
										row={ row }
										Page_Command={ this.Page_Command }
										history={ this.props.history }
										location={ this.props.location }
										match={ this.props.match }
										ref={ this.dynamicButtonsTop }
									/>
									<ErrorComponent error={error} />
								</div>
								: null
								}
								{ customView
								? React.createElement(customView, 
									{
										key             : MODULE_NAME + '.EditView.Inline', 
										MODULE_NAME     , 
										LAYOUT_NAME     : MODULE_NAME + '.EditView.Inline', 
										rowDefaultSearch: item, 
										callback        : this.editViewCallback, 
										history         : this.props.history, 
										location        : this.props.location, 
										match           : this.props.match, 
										ref             : this.editView
									})
								: <EditView
									key={ MODULE_NAME + '.EditView.Inline' }
									MODULE_NAME={ MODULE_NAME }
									LAYOUT_NAME={ MODULE_NAME + '.EditView.Inline' }
									rowDefaultSearch={ item }
									callback={ this.editViewCallback }
									history={ this.props.history }
									location={ this.props.location }
									match={ this.props.match }
									ref={ this.editView }
								/>
								}
								{ showBottomButtons
								? <DynamicButtons
									ButtonStyle="EditHeader"
									VIEW_NAME={ sNewRecordButtons }
									row={ row }
									Page_Command={ this.Page_Command }
									history={ this.props.history }
									location={ this.props.location }
									match={ this.props.match }
									ref={ this.dynamicButtonsBottom }
								/>
								: null
								}
							</div>
							: null
							}
							<SplendidGrid
								onLayoutLoaded={ this._onGridLayoutLoaded }
								MODULE_NAME={ PARENT_TYPE }
								RELATED_MODULE={ RELATED_MODULE }
								GRID_NAME={ GRID_NAME }
								TABLE_NAME={ TABLE_NAME }
								SORT_FIELD={ SORT_FIELD }
								SORT_DIRECTION={ SORT_DIRECTION }
								PRIMARY_FIELD={ PRIMARY_FIELD }
								PRIMARY_ID={ PRIMARY_ID }
								ADMIN_MODE={ false }
								archiveView={ archiveView }
								deferLoad={ true }
								disableView={ !ACLACCESS_Edit }
								disableEdit={ !ACLACCESS_Edit }
								disableRemove={ !ACLACCESS_Edit }
								cbRemove={ cbRemove }
								cbCustomLoad={ this.Load }
								onComponentComplete={ this._onComponentComplete }
								scrollable
								history={ this.props.history }
								location={ this.props.location }
								match={ this.props.match }
								ref={ this.splendidGrid }
							/>
						</React.Fragment>
						: null
						}
					</Content>
				</React.Fragment>
			);
		}
		else
		{
			return (
			<div id={ this.constructor.name + '_spinner' } style={ {textAlign: 'center'} }>
				<FontAwesomeIcon icon="spinner" spin={ true } size="5x" />
			</div>);
		}
	}
}

export default withRouter(UsersTeams);