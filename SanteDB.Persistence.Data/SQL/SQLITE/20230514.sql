/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230514-01" name="Update:20230514-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="sqlite">
 *	<summary>Update: Adds provenance data to the relationship verification table</summary>
 *	<isInstalled>select count(*) > 0 from patch_db_systbl WHERE PATCH_ID = '20230514-01'</isInstalled>
 * </feature>
 */
 
 -- Drop column REL_CLS

CREATE TEMPORARY TABLE temp AS
SELECT
  REL_VRFY_ID,
  REL_TYP_CD_ID,
  SRC_CLS_CD_ID,
  TRG_CLS_CD_ID,
  ERR_DESC,
  REL_CLS
FROM REL_VRFY_SYSTBL;

DROP TABLE REL_VRFY_SYSTBL;

CREATE TABLE REL_VRFY_SYSTBL (
	REL_VRFY_ID BLOB DEFAULT (RANDOMBLOB(16)) NOT NULL,
	REL_TYP_CD_ID BLOB NOT NULL,
	SRC_CLS_CD_ID BLOB,
	TRG_CLS_CD_ID BLOB,
	ERR_DESC VARCHAR(256) NOT NULL,
	REL_CLS INTEGER DEFAULT 1 NOT NULL CHECK (REL_CLS IN (1,2,3)),
	CRT_UTC BIGINT DEFAULT (UNIXEPOCH()) NOT NULL,
	CRT_PROV_ID BLOB(16) NOT NULL,
	OBSLT_UTC BIGINT,
	OBSLT_PROV_ID BLOB(16),
	CONSTRAINT PK_REL_VRFY_SYSTBL PRIMARY KEY (REL_TYP_CD_ID,SRC_CLS_CD_ID,TRG_CLS_CD_ID),
	CONSTRAINT FK_REL_SRC_CLS_CD_ID FOREIGN KEY (SRC_CLS_CD_ID) REFERENCES CD_TBL(CD_ID),
	CONSTRAINT FK_REL_TRG_CLS_CD_ID FOREIGN KEY (TRG_CLS_CD_ID) REFERENCES CD_TBL(CD_ID),
	CONSTRAINT FK_REL_VRFY_TYP_CD_ID FOREIGN KEY (REL_TYP_CD_ID) REFERENCES CD_TBL(CD_ID),
	CONSTRAINT FK_REL_VRFY_CRT_PROV_ID FOREIGN KEY (CRT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_REL_VRFY_OBSLT_PROV_ID FOREIGN KEY (OBSLT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID)
);

INSERT INTO REL_VRFY_SYSTBL
 (REL_VRFY_ID,
  REL_TYP_CD_ID,
  SRC_CLS_CD_ID,
  TRG_CLS_CD_ID,
  ERR_DESC,
  REL_CLS,
  CRT_PROV_ID)
SELECT
  REL_VRFY_ID,
  REL_TYP_CD_ID,
  SRC_CLS_CD_ID,
  TRG_CLS_CD_ID,
  ERR_DESC,
  REL_CLS,
  x'76A0DCFA90366E4AAF9EF1CD68E8C7E8'
FROM temp;
CREATE UNIQUE INDEX rel_vrfy_src_trg_unq ON rel_vrfy_systbl (rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id) WHERE (obslt_utc IS NULL);

DROP TABLE temp;
INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20230514-01', UNIXEPOCH(), 'Add create and obsolete times to database verify tables'); 