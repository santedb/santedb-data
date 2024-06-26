﻿/** 
 * <feature scope="SanteDB.Persistence.Data" id="20221027" name="Update:Foreign Data Manager"  invariantName="sqlite">
 *	<summary>Update:Adds Foreign Data Manager</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type='table' AND name='FD_STG_SYSTBL')</isInstalled>
 * </feature>
 */

 CREATE TABLE FD_STG_SYSTBL (
	FD_ID BLOB(16) NOT NULL DEFAULT (randomblob(16)), -- UNIQUE IDENTIFIER FOR THE FOREIGN DATA IMPORT
	CRT_UTC BIGINT NOT NULL DEFAULT (UNIXEPOCH()), -- CREATION TIME
	CRT_PROV_ID BLOB(16), -- CREATION USER
	OBSLT_UTC BIGINT, -- OBSOLETION TIME
	OBSLT_PROV_ID BLOB(16), -- OBSOLETION USER
	UPD_UTC BIGINT, -- UPDATE USER
	UPD_PROV_ID BLOB(16), -- UPDATED TIME
	NAME VARCHAR(128) NOT NULL, -- NAME OF THE ORIGINAL FILE / IMPORT STAGED
	STS_CS INTEGER NOT NULL DEFAULT 0, -- STATUS CODE OF THE IMPORT
	FD_MAP_ID BLOB(16), -- FOREIGN DATA MAP TO USE
	SRC_STR_ID BLOB(16) NOT NULL, -- SOURCE STREAM IDENTIFIER 
	REJ_STR_ID BLOB(16), -- REJECTION STREAM IDENTIFIER
	CONSTRAINT PK_FD_STG_SYSTBL PRIMARY KEY (FD_ID),
	CONSTRAINT FK_FD_STG_CRT_PROV FOREIGN KEY (CRT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_FD_STG_UPD_PROV FOREIGN KEY (UPD_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_FD_STG_OBSLT_PROV FOREIGN KEY (OBSLT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT CK_FD_STS_CS CHECK (STS_CS BETWEEN 0 AND 5)
 );

 CREATE TABLE FD_ISS_SYSTBL (
	FD_ISS_ID BLOB(16) NOT NULL DEFAULT (RANDOMBLOB(16)), -- UNIQUE IDENTIFIER OF THE ISSUE
	FD_ID BLOB(16) NOT NULL,  -- THE IDENTIFIER OF THE STAGED IMPORT TO WHICH THIS ISSUE APPLIES
	ISS_PRI INTEGER NOT NULL, -- THE PRIORITY OF THE ISSUE
	ISS_TXT VARCHAR(512) NOT NULL, -- THE TEXT OF THE ISSUE
	ISS_ID VARCHAR(128), -- ISSUE IDENTIFIER GIVEN BY THE IMPORTER
	ISS_TYP_CD BLOB(16), -- THE ISSUE TYPE
	CONSTRAINT PK_FD_ISS_SYSTBL PRIMARY KEY (FD_ISS_ID),
	CONSTRAINT FK_FD_ISS_ID FOREIGN KEY (FD_ID) REFERENCES FD_STG_SYSTBL(FD_ID)
 );

 