/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230330-01" name="Update:20230330-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="sqlite">
 *	<summary>Update: Adds concept set references to the database</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE tbl_name = 'CD_SET_ASSOC_TBL' AND sql LIKE '%SET_ID%')</isInstalled>
 * </feature>
 */
 
 CREATE TABLE CD_SET_COMP_ASSOC_TBL (
	SET_COMP_ID BLOB(16) NOT NULL DEFAULT (randomblob(16)),
	SET_ID BLOB(16) NOT NULL,
	TRG_SET_ID BLOB(16) NOT NULL,
	ROL_CS INT NOT NULL CHECK(ROL_CS IN (1,2)),
	CONSTRAINT PK_CD_SET_COMP_ASSOC_TBL PRIMARY KEY (SET_COMP_ID),
	CONSTRAINT FK_CD_SET_COMP_ASSOC_SRC_SET_TBL FOREIGN KEY (SET_ID) REFERENCES CD_SET_TBL(SET_ID),
	CONSTRAINT FK_CD_SET_COMP_ASSOC_TRG_SET_TBL FOREIGN KEY (TRG_SET_ID) REFERENCES CD_SET_TBL(SET_ID)
);--#!
