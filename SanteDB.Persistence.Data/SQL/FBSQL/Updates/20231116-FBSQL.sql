/** 
 * <feature scope="SanteDB.Persistence.Data" id="20231116-01" name="Update:20231116-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="FirebirdSQL">
 *	<summary>Update: Updates the storage of care plans and CDSS library logic</summary>
 *	<isInstalled>select ck_patch('20231116-01') from rdb$database</isInstalled>
 * </feature>
 */
 CREATE TABLE CP_TBL 
 (
	ACT_VRSN_ID UUID NOT NULL,
	TITLE BLOB SUB_TYPE TEXT, 
	PROG VARCHAR(256),
	CONSTRAINT PK_CP_TBL PRIMARY KEY (ACT_VRSN_ID),
	CONSTRAINT FK_CP_ACT_VRSN_TBL FOREIGN KEY (ACT_VRSN_ID) REFERENCES ACT_VRSN_TBL(ACT_VRSN_ID)
 );--#!

 -- CDSS LIBRARY STORAGE 
 CREATE TABLE CDSS_LIB_TBL (
	LIB_ID UUID NOT NULL,
	CLS VARCHAR(512) NOT NULL,
	IS_SYS BOOLEAN  DEFAULT FALSE NOT NULL,
	CONSTRAINT PK_CDSS_LIB_TBL PRIMARY KEY (LIB_ID)
);--#!

CREATE SEQUENCE CDSS_LIB_VRSN_SEQ;--#!

CREATE TABLE CDSS_LIB_VRSN_TBL (
	LIB_VRSN_ID UUID NOT NULL,
	LIB_ID UUID NOT NULL,
	VRSN_SEQ_ID BIGINT UNIQUE NOT NULL,
	RPLC_VRSN_ID UUID,
	HEAD BOOLEAN DEFAULT TRUE NOT NULL ,
	CRT_PROV_ID UUID NOT NULL,
	CRT_UTC TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL ,
	OBSLT_PROV_ID UUID,
	OBSLT_UTC TIMESTAMP,
	ID VARCHAR(256) NOT NULL,
	NAME VARCHAR(512) NOT NULL,
	VER VARCHAR(256),
	OID VARCHAR(256),
	DOC BLOB SUB_TYPE TEXT,
	DEF BLOB NOT NULL,
	CONSTRAINT PK_CDSS_LIB_VRSN_TBL PRIMARY KEY (LIB_VRSN_ID),
	CONSTRAINT FK_CDSS_LIB_LIB_TBL FOREIGN KEY (LIB_ID) REFERENCES CDSS_LIB_TBL(LIB_ID),
	CONSTRAINT FK_CDSS_LIB_VRSN_CRT_PROV_TBL FOREIGN KEY (CRT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_CDSS_LIB_VRSN_OBSLT_PROV_TBL FOREIGN KEY (OBSLT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_CDSS_LIB_VRSN_RPLC_TBL FOREIGN KEY (RPLC_VRSN_ID) REFERENCES CDSS_LIB_VRSN_TBL(LIB_VRSN_ID),
	CONSTRAINT FK_OBSLT_PROV_LOGIC CHECK ((OBSLT_PROV_ID IS NULL AND OBSLT_UTC IS NULL) OR (OBSLT_PROV_ID IS NOT NULL AND OBSLT_UTC IS NOT NULL))
);--#!

CREATE TRIGGER TG_CDSS_LIB_VRSN_TBL_SEQ FOR CDSS_LIB_VRSN_TBL ACTIVE BEFORE INSERT POSITION 0 AS BEGIN
	NEW.VRSN_SEQ_ID = NEXT VALUE FOR CDSS_LIB_VRSN_SEQ;
END;--#!
CREATE UNIQUE INDEX CDSS_LIB_ID_IDX ON CDSS_LIB_VRSN_TBL(ID);--#!
CREATE UNIQUE INDEX CDSS_LIB_NAME_IDX ON CDSS_LIB_VRSN_TBL(NAME);--#!
CREATE INDEX CDSS_LIB_NAM_IDX ON CDSS_LIB_VRSN_TBL(NAME);--#!


SELECT REG_PATCH('20231116-01') FROM RDB$DATABASE; --#!