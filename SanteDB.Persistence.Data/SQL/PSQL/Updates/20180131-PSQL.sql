﻿/** 
 * <feature scope="SanteDB.Persistence.Data" id="20180131-01" name="Update:20180131-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="npgsql">
 *	<summary>Update: Add relationship "replaces" between all entities of the same class</summary>
 *	<remarks>Any entity is technically allowed to replace itself :)</remarks>
 *	<isInstalled>select ck_patch('20180131-01')</isInstalled>
 * </feature>
 */

BEGIN TRANSACTION ;

INSERT INTO ent_rel_vrfy_cdtbl (rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id, err_desc) 
SELECT 'd1578637-e1cb-415e-b319-4011da033813', cd_id, cd_id, 'err_ReplaceOnlySameType' FROM cd_set_mem_vw WHERE set_mnemonic = 'EntityClass';

CREATE INDEX act_rel_typ_cd_idx ON act_rel_tbl(rel_typ_cd_id);
SELECT REG_PATCH('20180131-01');
COMMIT;
