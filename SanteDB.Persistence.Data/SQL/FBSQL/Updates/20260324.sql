/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260812-01" name="Update:20260812-01"   invariantName="FirebirdSQL" >
 *	<summary>Update: Add classification code to security policy</summary>
 *	<isInstalled>select ck_patch('20260812-01') from rdb$database</isInstalled>
 * </feature>
 */

ALTER TABLE SEC_POL_TBL ADD CLS_CD_ID UUID;--#!
ALTER TABLE SEC_POL_TBL ADD CONSTRAINT FK_SEC_POL_CLS_CD_ID FOREIGN KEY (CLS_CD_ID) REFERENCES CD_TBL(CD_ID);--#!
CREATE UNIQUE INDEX uq_ent_pol_assoc_idx ON ent_pol_assoc_tbl (ent_id, pol_id);--#!
CREATE UNIQUE INDEX uq_act_pol_assoc_idx ON act_pol_assoc_tbl (act_id, pol_id);--#!
 SELECT REG_PATCH('20260812-01') FROM RDB$DATABASE;