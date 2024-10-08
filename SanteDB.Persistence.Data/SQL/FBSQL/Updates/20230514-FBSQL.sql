/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230514-01" name="Update:20230514-01"   invariantName="FirebirdSQL">
 *	<summary>Update: Added creation time fields to the REL_VRFY_SYSTBL</summary>
 *	<isInstalled>select ck_patch('20230514-01') from rdb$database</isInstalled>
 * </feature>
 */
 
 ALTER TABLE REL_VRFY_SYSTBL ADD CRT_UTC TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL ;--#!
 ALTER TABLE REL_VRFY_SYSTBL ADD CRT_PROV_ID UUID;--#!
 UPDATE REL_VRFY_SYSTBL SET CRT_PROV_ID = CHAR_TO_UUID('fadca076-3690-4a6e-af9e-f1cd68e8c7e8');--#!
 CREATE TRIGGER TG_REL_VRFY_SYSTBL_CRT_PROV FOR REL_VRFY_SYSTBL ACTIVE BEFORE INSERT POSITION 0 AS BEGIN
	NEW.CRT_PROV_ID = CHAR_TO_UUID('fadca076-3690-4a6e-af9e-f1cd68e8c7e8');
END;--#!
 ALTER TABLE REL_VRFY_SYSTBL ALTER CRT_PROV_ID SET NOT NULL;--#!
 ALTER TABLE REL_VRFY_SYSTBL ADD CONSTRAINT FK_CRT_PROV_TBL FOREIGN KEY (CRT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID);--#!
 ALTER TABLE REL_VRFY_SYSTBL ADD OBSLT_UTC TIMESTAMP;--#!
 ALTER TABLE REL_VRFY_SYSTBL ADD OBSLT_PROV_ID UUID;--#!
 ALTER TABLE REL_VRFY_SYSTBL ADD CONSTRAINT FK_OBSLT_PROV_TBL FOREIGN KEY (OBSLT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID); --#!
 DROP INDEX rel_vrfy_src_trg_unq;--#!
ALTER TABLE BI_DM_REG_SYSTBL ADD HASH BLOB;	--#!
SELECT REG_PATCH('20230514-01') FROM RDB$DATABASE; 
