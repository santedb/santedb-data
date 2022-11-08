/** 
 * <feature scope="SanteDB.Persistence.Data" id="20221027" name="Update:Session User Index" applyRange="0.2.0.0-0.9.0.0" invariantName="sqlite">
 *	<summary>Update:Adds index for user key in session table</summary>
 *	<remarks>Adds an index for the </remarks>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type='index' AND name='SEC_SES_USR_ID_IDX')</isInstalled>
 * </feature>
 */
 -- OPTIONAL
 CREATE INDEX IF NOT EXISTS SEC_SES_USR_ID_IDX ON SEC_SES_TBL(USR_ID); --#!

 
UPDATE ent_vrsn_tbl SET cls_cd_id = x'BA002B6A1B502345B57CF96D8AE44684' WHERE ent_id = x'36085FB5E640E24E952227E3F8BFE532';--#!

INSERT INTO ent_name_tbl (name_id, ent_id, efft_vrsn_seq_id, use_cd_id) VALUES (x'910BFFE2818FB34BB83472835146B177', x'36085FB5E640E24E952227E3F8BFE532', 1, x'3A58C91E19B0AA4BB856B99CAF368656');--#!
INSERT INTO ent_name_cmp_tbl (name_id, typ_cd_id, val) VALUES (x'910BFFE2818FB34BB83472835146B177', x'E2BD642F96A60A4B9690B21EBD7E5092', 'Wile');--#!
INSERT INTO ent_name_cmp_tbl (name_id, typ_cd_id, val) VALUES (x'910BFFE2818FB34BB83472835146B177', x'E2BD642F96A60A4B9690B21EBD7E5092', 'E.');--#!
INSERT INTO ent_name_cmp_tbl (name_id, typ_cd_id, val) VALUES (x'910BFFE2818FB34BB83472835146B177', x'5584B92961EDF849A1612D73363E1DF0', 'Coyote');--#!