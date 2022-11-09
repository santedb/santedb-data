/** 
 * <feature scope="SanteDB.Persistence.Data" id="20221027-01" name="Update:20221027-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="npgsql">
 *	<summary>Update: Index to session table for fetching user sessions</summary>
 *	<isInstalled>select ck_patch('20221027-01')</isInstalled>
 * </feature>
 */

CREATE INDEX sec_ses_usr_id_exp_utc_idx ON sec_ses_tbl USING btree (usr_id, exp_utc);
INSERT INTO ent_name_tbl (name_id, ent_id, efft_vrsn_seq_id, use_cd_id) VALUES ('E2FF0B91-8F81-4BB3-B834-72835146B177', 'b55f0836-40e6-4ee2-9522-27e3f8bfe532', 1, '1EC9583A-B019-4BAA-B856-B99CAF368656');
INSERT INTO ent_name_cmp_tbl (name_id, typ_cd_id, val) VALUES ('E2FF0B91-8F81-4BB3-B834-72835146B177', '2F64BDE2-A696-4B0A-9690-B21EBD7E5092', 'Wile');
INSERT INTO ent_name_cmp_tbl (name_id, typ_cd_id, val) VALUES ('E2FF0B91-8F81-4BB3-B834-72835146B177', '2F64BDE2-A696-4B0A-9690-B21EBD7E5092', 'E.');
INSERT INTO ent_name_cmp_tbl (name_id, typ_cd_id, val) VALUES ('E2FF0B91-8F81-4BB3-B834-72835146B177', '29B98455-ED61-49F8-A161-2D73363E1DF0', 'Coyote');

SELECT REG_PATCH('20221027-01'); 