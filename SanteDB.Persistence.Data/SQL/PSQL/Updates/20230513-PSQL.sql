/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230514-01" name="Update:20230514-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="npgsql">
 *	<summary>Update: Adds provenance data to the relationship verification table</summary>
 *	<isInstalled>select ck_patch('20230514-01')</isInstalled>
 * </feature>
 */
 ALTER TABLE REL_VRFY_SYSTBL ADD CRT_UTC TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL ;
 ALTER TABLE REL_VRFY_SYSTBL ADD CRT_PROV_ID UUID DEFAULT 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8' NOT NULL;
 ALTER TABLE REL_VRFY_SYSTBL ADD CONSTRAINT FK_CRT_PROV_TBL FOREIGN KEY (CRT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID);
 ALTER TABLE REL_VRFY_SYSTBL ADD OBSLT_UTC TIMESTAMPTZ;
 ALTER TABLE REL_VRFY_SYSTBL ADD OBSLT_PROV_ID UUID;
 ALTER TABLE REL_VRFY_SYSTBL ADD CONSTRAINT FK_OBSLT_PROV_TBL FOREIGN KEY (OBSLT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID);
 DROP INDEX rel_vrfy_src_trg_unq;
 CREATE UNIQUE INDEX rel_vrfy_src_trg_unq ON rel_vrfy_systbl USING btree (rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id) WHERE (obslt_utc IS NULL);

SELECT REG_PATCH('20230514-01'); 