/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230227-01" name="Update:20230227-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="FirebirdSQL">
 *	<summary>Update: Migrate old class codes</summary>
 *	<isInstalled>select ck_patch('20230227-01') from RDB$DATABASE</isInstalled>
 * </feature>
 */
UPDATE ENT_VRSN_TBL SET CLS_CD_ID = char_to_uuid('4d1a5c28-deb7-411e-b75f-d524f90dfa63') WHERE CLS_CD_ID = char_to_uuid('8CF4B0B0-84E5-4122-85FE-6AFA8240C218');--#!

INSERT INTO REL_VRFY_SYSTBL (rel_vrfy_id, rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id, err_desc) 
SELECT uuid_generate_v1(), rel_typ_cd_id, CASE WHEN SRC_CLS_CD_ID = char_to_uuid('8CF4B0B0-84E5-4122-85FE-6AFA8240C218') THEN char_to_uuid('4d1a5c28-deb7-411e-b75f-d524f90dfa63') ELSE SRC_CLS_CD_ID END,
CASE WHEN TRG_CLS_CD_ID = char_to_uuid('8CF4B0B0-84E5-4122-85FE-6AFA8240C218') THEN char_to_uuid('4d1a5c28-deb7-411e-b75f-d524f90dfa63') ELSE TRG_CLS_CD_ID END, err_desc
FROM REL_VRFY_SYSTBL
WHERE SRC_CLS_CD_ID = char_to_uuid('8CF4B0B0-84E5-4122-85FE-6AFA8240C218') OR TRG_CLS_CD_ID = char_to_uuid('8CF4B0B0-84E5-4122-85FE-6AFA8240C218')
AND NOT EXISTS (SELECT 1 FROM REL_VRFY_SYSTBL D WHERE 
	D.SRC_CLS_CD_ID = CASE WHEN SRC_CLS_CD_ID = char_to_uuid('8CF4B0B0-84E5-4122-85FE-6AFA8240C218') THEN char_to_uuid('4d1a5c28-deb7-411e-b75f-d524f90dfa63') ELSE SRC_CLS_CD_ID END
	AND D.TRG_CLS_CD_ID = CASE WHEN TRG_CLS_CD_ID = char_to_uuid('8CF4B0B0-84E5-4122-85FE-6AFA8240C218') THEN char_to_uuid('4d1a5c28-deb7-411e-b75f-d524f90dfa63') ELSE TRG_CLS_CD_ID END 
	AND D.REL_TYP_CD_ID = REL_TYP_CD_ID)
	;--#!
DELETE FROM REL_VRFY_SYSTBL WHERE SRC_CLS_CD_ID = char_to_uuid('8CF4B0B0-84E5-4122-85FE-6AFA8240C218') OR TRG_CLS_CD_ID = char_to_uuid('8CF4B0B0-84E5-4122-85FE-6AFA8240C218') --#!


-- FIX COUNTY OR PARISH
UPDATE ENT_VRSN_TBL SET CLS_CD_ID = char_to_uuid('6eefee7d-dff5-46d3-a6a7-171ef93879c7') WHERE CLS_CD_ID = char_to_uuid('d9489d56-ddac-4596-b5c6-8f41d73d8dc5');--#!
INSERT INTO REL_VRFY_SYSTBL (rel_vrfy_id, rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id, err_desc) 
SELECT uuid_generate_v1(), rel_typ_cd_id, CASE WHEN SRC_CLS_CD_ID = char_to_uuid('d9489d56-ddac-4596-b5c6-8f41d73d8dc5') THEN char_to_uuid('6eefee7d-dff5-46d3-a6a7-171ef93879c7') ELSE SRC_CLS_CD_ID END,
CASE WHEN TRG_CLS_CD_ID = char_to_uuid('d9489d56-ddac-4596-b5c6-8f41d73d8dc5') THEN char_to_uuid('6eefee7d-dff5-46d3-a6a7-171ef93879c7') ELSE TRG_CLS_CD_ID END, err_desc
FROM REL_VRFY_SYSTBL
WHERE SRC_CLS_CD_ID = char_to_uuid('d9489d56-ddac-4596-b5c6-8f41d73d8dc5') OR TRG_CLS_CD_ID = char_to_uuid('d9489d56-ddac-4596-b5c6-8f41d73d8dc5')
AND NOT EXISTS (SELECT 1 FROM REL_VRFY_SYSTBL D WHERE 
	D.SRC_CLS_CD_ID = CASE WHEN SRC_CLS_CD_ID = char_to_uuid('d9489d56-ddac-4596-b5c6-8f41d73d8dc5') THEN char_to_uuid('6eefee7d-dff5-46d3-a6a7-171ef93879c7') ELSE SRC_CLS_CD_ID END
	AND D.TRG_CLS_CD_ID = CASE WHEN TRG_CLS_CD_ID = char_to_uuid('d9489d56-ddac-4596-b5c6-8f41d73d8dc5') THEN char_to_uuid('6eefee7d-dff5-46d3-a6a7-171ef93879c7') ELSE TRG_CLS_CD_ID END 
	AND D.REL_TYP_CD_ID = REL_TYP_CD_ID)
	;--#!
DELETE FROM REL_VRFY_SYSTBL WHERE SRC_CLS_CD_ID = char_to_uuid('d9489d56-ddac-4596-b5c6-8f41d73d8dc5') OR TRG_CLS_CD_ID = char_to_uuid('d9489d56-ddac-4596-b5c6-8f41d73d8dc5') --#!

 
SELECT REG_PATCH('20230227-01') FROM RDB$DATABASE; --#!

