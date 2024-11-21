/** 
 * <feature scope="SanteDB.Persistence.Data" id="20241118" name="Update:20241118"  invariantName="sqlite" environment="Server">
 *	<summary>Updates the security challenges to include additional text</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM patch_db_systbl WHERE patch_id='20241118')</isInstalled>
 * </feature>
 */
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (randomblob(16), 'security.challenge.text4', x'76A0DCFA90366E4AAF9EF1CD68E8C7E8');
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (randomblob(16), 'security.challenge.text5', x'76A0DCFA90366E4AAF9EF1CD68E8C7E8');
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (randomblob(16), 'security.challenge.text6', x'76A0DCFA90366E4AAF9EF1CD68E8C7E8');
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (randomblob(16), 'security.challenge.text7', x'76A0DCFA90366E4AAF9EF1CD68E8C7E8');
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (randomblob(16), 'security.challenge.text8', x'76A0DCFA90366E4AAF9EF1CD68E8C7E8');
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (randomblob(16), 'security.challenge.text9', x'76A0DCFA90366E4AAF9EF1CD68E8C7E8');
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (randomblob(16), 'security.challenge.text10', x'76A0DCFA90366E4AAF9EF1CD68E8C7E8');
INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20241118', unixepoch(), 'Updates the security challenges to include additional text');