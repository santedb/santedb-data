/** 
 * <feature scope="SanteDB.Persistence.Data" id="20250530" name="Update:20250530"  invariantName="sqlite">
 *	<summary>Update: Update: Add patient registration back entry relationship rules</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM REL_VRFY_SYSTBL WHERE REL_TYP_CD_ID = x'0F54B9788B436F4B8D83AAF4979DBC64' AND src_cls_cd_id = x'58D3E86B91F53A4A9A571889B0147C7E')</isInstalled>
 * </feature>
 */
 
 INSERT OR IGNORE INTO REL_VRFY_SYSTBL (rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id, err_desc, rel_cls) VALUES 
	(x'0F54B9788B436F4B8D83AAF4979DBC64', x'58D3E86B91F53A4A9A571889B0147C7E', null, 'Registration=[HasComponent]=>*', 2);