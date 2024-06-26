/** 
 * <feature scope="SanteDB.Persistence.Data" id="20250521-03" name="Update:20250521-03"   invariantName="FirebirdSQL">
 *	<summary>Update: Updates the storage extension types</summary>
 *	<isInstalled>select ck_patch('20250521-03') from rdb$database</isInstalled>
 * </feature>
 */

 ALTER TABLE EXT_TYP_TBL ADD EXT_URI VARCHAR(512);--#!
 DROP INDEX EXT_TYP_NAME_IDX;--#!
 UPDATE EXT_TYP_TBL SET EXT_URI = EXT_NAME; --#!
 CREATE UNIQUE INDEX EXT_TYP_URI_IDX ON EXT_TYP_TBL(EXT_URI);--#!
 CREATE TABLE EXT_TYP_SCP_TBL (
	EXT_TYP_ID UUID NOT NULL, 
	CLS_CD_ID UUID NOT NULL,
	CONSTRAINT PK_EXT_TYP_SCP_TBL PRIMARY KEY (EXT_TYP_ID, CLS_CD_ID),
	CONSTRAINT FK_EXT_TYP_SCP_EXT_TYP FOREIGN KEY (EXT_TYP_ID) REFERENCES EXT_TYP_TBL(EXT_TYP_ID),
	CONSTRAINT FK_EXT_TYP_SCP_CLS_CD FOREIGN KEY (CLS_CD_ID) REFERENCES CD_TBL(CD_ID),
	CONSTRAINT CK_EXT_TYP_CLS_CD CHECK (IS_CD_SET_MEM(CLS_CD_ID, 'ActClass') OR IS_CD_SET_MEM(CLS_CD_ID, 'EntityClass'))
);--#!

SELECT REG_PATCH('20250521-03') FROM RDB$DATABASE; --#!
