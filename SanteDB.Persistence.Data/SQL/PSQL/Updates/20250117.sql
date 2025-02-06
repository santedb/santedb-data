/** 
 * <feature scope="SanteDB.Persistence.Data" id="20250117-01" name="Update:20250117-01"   invariantName="npgsql" >
 *	<summary>Update: Adds the replaces relationship for acts</summary>
 *	<isInstalled>select ck_patch('20250117-01')</isInstalled>
 * </feature>
 */

 
INSERT INTO REL_VRFY_SYSTBL (rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id, err_desc, rel_cls) 
SELECT 'd1578637-e1cb-415e-b319-4011da033813', cd_id, cd_id, 'err_ReplaceOnlySameType', 2 FROM cd_set_mem_vw WHERE set_mnemonic ='ActClass'
ON CONFLICT DO NOTHING;

SELECT REG_PATCH('20250117-01');