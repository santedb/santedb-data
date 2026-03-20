/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260330-02" name="Update:20260330-02"   invariantName="sqlite" environment="Server" >
 *	<summary>Update: Initialize mailboxes for users</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM patch_db_systbl WHERE patch_id='20260330-02')</isInstalled>
 * </feature>
 */
 

INSERT OR IGNORE INTO mail_box_tbl (own_id, name, crt_prov_id)
SELECT usr_id, 'Inbox', x'76A0DCFA90366E4AAF9EF1CD68E8C7E8'
FROM sec_usr_tbl
WHERE usr_name NOT IN ('SYSTEM','ANONYMOUS');
INSERT OR IGNORE INTO mail_box_tbl (own_id, name, crt_prov_id)
SELECT usr_id, 'Deleted', x'76A0DCFA90366E4AAF9EF1CD68E8C7E8'
FROM sec_usr_tbl
WHERE usr_name NOT IN ('SYSTEM','ANONYMOUS');
INSERT OR IGNORE INTO mail_box_tbl (own_id, name, crt_prov_id)
SELECT usr_id, 'Sent', x'76A0DCFA90366E4AAF9EF1CD68E8C7E8'
FROM sec_usr_tbl
WHERE usr_name NOT IN ('SYSTEM','ANONYMOUS');
INSERT OR IGNORE INTO mail_box_tbl (own_id, name, crt_prov_id)
SELECT dev_id, 'Inbox', x'76A0DCFA90366E4AAF9EF1CD68E8C7E8'
FROM sec_dev_tbl
WHERE dev_pub_id NOT IN ('SYSTEM','ANONYMOUS');

INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20260330-02', UNIXEPOCH(), 'Initialize Mail On Server Only');--#!
