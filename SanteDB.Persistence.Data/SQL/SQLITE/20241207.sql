/** 
 * <feature scope="SanteDB.Persistence.Data" id="20241207" name="Update:20241207"  invariantName="sqlite">
 *	<summary>Update: Updates the concept relationship tables to support flow relationships</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE name='TPL_VW_DEF_TBL')</isInstalled>
 * </feature>
 */
 
CREATE TABLE TPL_VW_DEF_TBL (
	TPL_ID blob(16) DEFAULT (randomblob(16)) NOT NULL,
	CRT_UTC BIGINT DEFAULT (unixepoch()) NOT NULL,
	CRT_PROV_ID blob(16) NOT NULL,
	UPD_UTC BIGINT,
	UPD_PROV_ID blob(16),
	OBSLT_UTC BIGINT,
	OBSLT_PROV_ID blob(16),
	TPL_NAME VARCHAR(256) NOT NULL,
	DESCR BLOB SUB_TYPE TEXT, 
	OID VARCHAR(512) NOT NULL,
	MNEMONIC VARCHAR(512) NOT NULL,
	AC BOOL NOT NULL DEFAULT TRUE,
	RO BOOL NOT NULL DEFAULT FALSE,
	PUB BOOL NOT NULL DEFAULT TRUE,
	VER INT NOT NULL DEFAULT 1,
	DEF BLOB NOT NULL,
	CONSTRAINT PK_TPL_VW_DEF_TBL PRIMARY KEY (TPL_ID),
	CONSTRAINT FK_TPL_VW_DEF_DEF FOREIGN KEY (TPL_ID) REFERENCES TPL_DEF_TBL(TPL_ID),
	CONSTRAINT FK_TPL_VW_CRT_PROV_ID FOREIGN KEY (CRT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_TPL_VW_UPD_PROV_ID FOREIGN KEY (UPD_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_TPL_VW_OBSLT_PROV_ID FOREIGN KEY (OBSLT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT CK_TPL_VW_UPD CHECK ((UPD_PROV_ID IS NULL AND UPD_UTC IS NULL) OR (UPD_PROV_ID IS NOT NULL AND UPD_UTC IS NOT NULL)),
	CONSTRAINT FK_TPL_VW_OBSLT CHECK ((OBSLT_PROV_ID IS NULL AND OBSLT_UTC IS NULL) OR (OBSLT_PROV_ID IS NOT NULL AND OBSLT_UTC IS NOT NULL))
);

CREATE UNIQUE INDEX TPL_VW_DEF_OID_UQ ON TPL_VW_DEF_TBL(OID) WHERE (OBSLT_UTC IS NULL);
CREATE UNIQUE INDEX TPL_VW_DEF_MNEMONIC_UQ ON TPL_VW_DEF_TBL(MNEMONIC) WHERE (OBSLT_UTC IS NULL);