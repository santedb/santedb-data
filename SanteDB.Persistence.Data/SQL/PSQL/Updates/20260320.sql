/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260330-02" name="Update:20260330-02"   invariantName="npgsql" environment="Server">
 *	<summary>Update: Setup mailboxes for existing users</summary>
 *	<isInstalled>select ck_patch('20260330-02')</isInstalled>
 * </feature>
 */
 

-- CREATE MAILBOXES FOR EXISTING USERS
INSERT INTO mail_box_tbl (own_id, name, crt_prov_id)
SELECT usr_id, 'Inbox', 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8'
FROM sec_usr_tbl
ON CONFLICT DO NOTHING;
INSERT INTO mail_box_tbl (own_id, name, crt_prov_id)
SELECT usr_id, 'Deleted', 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8'
FROM sec_usr_tbl
ON CONFLICT DO NOTHING;
INSERT INTO mail_box_tbl (own_id, name, crt_prov_id)
SELECT usr_id, 'Sent', 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8'
FROM sec_usr_tbl
ON CONFLICT DO NOTHING;
INSERT INTO mail_box_tbl (own_id, name, crt_prov_id)
SELECT dev_id, 'Inbox', 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8'
FROM sec_dev_tbl
WHERE dev_pub_id NOT IN ('SYSTEM','ANONYMOUS')
ON CONFLICT DO NOTHING;

 SELECT REG_PATCH('20260330-02');