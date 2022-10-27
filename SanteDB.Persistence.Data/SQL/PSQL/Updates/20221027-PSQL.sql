/** 
 * <feature scope="SanteDB.Persistence.Data" id="20221027-01" name="Update:20221027-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="npgsql">
 *	<summary>Update: Index to session table for fetching user sessions</summary>
 *	<isInstalled>select ck_patch('20221027-01')</isInstalled>
 * </feature>
 */

CREATE INDEX sec_ses_usr_id_exp_utc_idx ON sec_ses_tbl USING btree (usr_id, exp_utc);

SELECT REG_PATCH('20221027-01'); 