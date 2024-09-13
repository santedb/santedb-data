/** 
 * <feature scope="SanteDB.Persistence.Data" id="20221027" name="Update:Session User Index"  invariantName="sqlite">
 *	<summary>Update:Adds index for user key in session table</summary>
 *	<remarks>Adds an index for the </remarks>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE name='SEC_SES_USR_ID_IDX')</isInstalled>
 * </feature>
 */
 -- OPTIONAL
 CREATE INDEX IF NOT EXISTS SEC_SES_USR_ID_IDX ON SEC_SES_TBL(USR_ID); --#!

 -- OPTIONAL
INSERT INTO ent_name_tbl (name_id, ent_id, efft_vrsn_seq_id, use_cd_id) VALUES (x'910BFFE2818FB34BB83472835146B177', x'36085FB5E640E24E952227E3F8BFE532', 1, x'3A58C91E19B0AA4BB856B99CAF368656');--#!
-- OPTIONAL
INSERT INTO ent_name_cmp_tbl (name_cmp_id, name_id, typ_cd_id, val) VALUES (x'A0ECDA90E199EE119023A7B7CF774235', x'910BFFE2818FB34BB83472835146B177', x'E2BD642F96A60A4B9690B21EBD7E5092', 'Wile');--#!
-- OPTIONAL
INSERT INTO ent_name_cmp_tbl (name_cmp_id, name_id, typ_cd_id, val) VALUES (x'826EDB90E199EE119024B3814DB4D50F', x'910BFFE2818FB34BB83472835146B177', x'E2BD642F96A60A4B9690B21EBD7E5092', 'E.');--#!
-- OPTIONAL
INSERT INTO ent_name_cmp_tbl (name_cmp_id, name_id, typ_cd_id, val) VALUES (x'F898DB90E199EE1190250FD87E3E2326', x'910BFFE2818FB34BB83472835146B177', x'5584B92961EDF849A1612D73363E1DF0', 'Coyote');--#!
