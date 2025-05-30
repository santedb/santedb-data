/** 
 * <feature scope="SanteDB.Persistence.Data" id="20250530-01" name="Update:20250530-01"   invariantName="npgsql" >
 *	<summary>Update: Add patient registration back entry relationship rules</summary>
 *	<isInstalled>select ck_patch('20250530-01')</isInstalled>
 * </feature>
 */

 INSERT INTO REL_VRFY_SYSTBL (rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id, err_desc, rel_cls) VALUES 
	('78b9540f-438b-4b6f-8d83-aaf4979dbc64', '6be8d358-f591-4a3a-9a57-1889b0147c7e', null, 'Registration=[HasComponent]=>*', 2)
	ON CONFLICT DO NOTHING;

 SELECT REG_PATCH('20250530-01');