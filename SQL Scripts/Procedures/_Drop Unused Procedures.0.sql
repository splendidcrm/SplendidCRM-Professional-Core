if exists (select * from INFORMATION_SCHEMA.ROUTINES where ROUTINE_NAME = 'spAPPOINTMENTS_GOOGLE_Insert' and ROUTINE_TYPE = 'PROCEDURE')
	Drop Procedure dbo.spAPPOINTMENTS_GOOGLE_Insert;
GO
