/** 
 * <feature scope="SanteDB.Persistence.Data" id="20241118-01" name="Update:20241118-01"   invariantName="npgsql"  environment="Server Gateway">
 *	<summary>Update: Updates the security challenges to include additional text</summary>
 *	<isInstalled>select ck_patch('20241118-01')</isInstalled>
 * </feature>
 */

INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (uuid_generate_v1(), 'security.challenge.text4', 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8');
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (uuid_generate_v1(), 'security.challenge.text5', 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8');
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (uuid_generate_v1(), 'security.challenge.text6', 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8');
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (uuid_generate_v1(), 'security.challenge.text7', 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8');
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (uuid_generate_v1(), 'security.challenge.text8', 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8');
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (uuid_generate_v1(), 'security.challenge.text9', 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8');
INSERT INTO sec_chl_tbl (chl_id, chl_txt, crt_prov_id) VALUES (uuid_generate_v1(), 'security.challenge.text10', 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8');
SELECT REG_PATCH('20241118-01');