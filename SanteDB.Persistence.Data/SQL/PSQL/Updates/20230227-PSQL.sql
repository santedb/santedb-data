/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230227-01" name="Update:20230227-01"   invariantName="npgsql">
 *	<summary>Update: Migrate Nationality and VIP to person</summary>
 *	<isInstalled>select ck_patch('20230227-01')</isInstalled>
 * </feature>
 */
UPDATE ENT_VRSN_TBL SET CLS_CD_ID = '4d1a5c28-deb7-411e-b75f-d524f90dfa63' WHERE CLS_CD_ID = '8CF4B0B0-84E5-4122-85FE-6AFA8240C218';

INSERT INTO cd_set_mem_assoc_tbl (set_id, cd_id) VALUES ('4e6da567-0094-4f23-8555-11da499593af','6eefee7d-dff5-46d3-a6a7-171ef93879c7') ON CONFLICT DO NOTHING;
INSERT INTO cd_set_mem_assoc_tbl (set_id, cd_id) VALUES ('4e6da567-0094-4f23-8555-11da499593af','4d1a5c28-deb7-411e-b75f-d524f90dfa63') ON CONFLICT DO NOTHING;
insert into cd_name_tbl (cd_id, efft_vrsn_seq_id, lang_cs, val) values ('4d1a5c28-deb7-411e-b75f-d524f90dfa63', 104, 'en', 'State or Province') ON CONFLICT DO NOTHING;

-- FIX STATE OR PROVINCE
INSERT INTO REL_VRFY_SYSTBL (rel_vrfy_id, rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id, err_desc) 
SELECT uuid_generate_v1(), rel_typ_cd_id, CASE WHEN SRC_CLS_CD_ID = '8CF4B0B0-84E5-4122-85FE-6AFA8240C218' THEN '4d1a5c28-deb7-411e-b75f-d524f90dfa63' ELSE SRC_CLS_CD_ID END,
CASE WHEN TRG_CLS_CD_ID = '8CF4B0B0-84E5-4122-85FE-6AFA8240C218' THEN '4d1a5c28-deb7-411e-b75f-d524f90dfa63' ELSE TRG_CLS_CD_ID END, err_desc
FROM REL_VRFY_SYSTBL
WHERE SRC_CLS_CD_ID = '8CF4B0B0-84E5-4122-85FE-6AFA8240C218' OR TRG_CLS_CD_ID = '8CF4B0B0-84E5-4122-85FE-6AFA8240C218' 
ON CONFLICT DO NOTHING;

DELETE FROM REL_VRFY_SYSTBL WHERE SRC_CLS_CD_ID = '8CF4B0B0-84E5-4122-85FE-6AFA8240C218' OR TRG_CLS_CD_ID = '8CF4B0B0-84E5-4122-85FE-6AFA8240C218' ;

-- FIX COUNTY OR PARISH
UPDATE ENT_VRSN_TBL SET CLS_CD_ID = ('6eefee7d-dff5-46d3-a6a7-171ef93879c7') WHERE CLS_CD_ID = ('d9489d56-ddac-4596-b5c6-8f41d73d8dc5');
INSERT INTO REL_VRFY_SYSTBL (rel_vrfy_id, rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id, err_desc) 
SELECT uuid_generate_v1(), rel_typ_cd_id, CASE WHEN SRC_CLS_CD_ID = ('d9489d56-ddac-4596-b5c6-8f41d73d8dc5') THEN ('6eefee7d-dff5-46d3-a6a7-171ef93879c7') ELSE SRC_CLS_CD_ID END,
CASE WHEN TRG_CLS_CD_ID = ('d9489d56-ddac-4596-b5c6-8f41d73d8dc5') THEN ('6eefee7d-dff5-46d3-a6a7-171ef93879c7') ELSE TRG_CLS_CD_ID END, err_desc
FROM REL_VRFY_SYSTBL
WHERE SRC_CLS_CD_ID = ('d9489d56-ddac-4596-b5c6-8f41d73d8dc5') OR TRG_CLS_CD_ID = ('d9489d56-ddac-4596-b5c6-8f41d73d8dc5')
ON CONFLICT DO NOTHING;
DELETE FROM REL_VRFY_SYSTBL WHERE SRC_CLS_CD_ID = ('d9489d56-ddac-4596-b5c6-8f41d73d8dc5') OR TRG_CLS_CD_ID = ('d9489d56-ddac-4596-b5c6-8f41d73d8dc5');


delete from cd_set_mem_assoc_tbl where set_id = '3d02fb53-1133-406d-a06f-7d47a556f3bc' and cd_id = '8cf4b0b0-84e5-4122-85fe-6afa8240c218';
delete from cd_set_mem_assoc_tbl where set_id = '4e6da567-0094-4f23-8555-11da499593af' and cd_id = '8cf4b0b0-84e5-4122-85fe-6afa8240c218';
delete from cd_set_mem_assoc_tbl where set_id = '3d02fb53-1133-406d-a06f-7d47a556f3bc' and cd_id = 'd9489d56-ddac-4596-b5c6-8f41d73d8dc5';
delete from cd_set_mem_assoc_tbl where set_id = '91dec343-1324-4fa4-8cea-4a1d0439908a' and cd_id = 'd9489d56-ddac-4596-b5c6-8f41d73d8dc5';
delete from cd_set_mem_assoc_tbl where set_id = '4e6da567-0094-4f23-8555-11da499593af' and cd_id = 'd9489d56-ddac-4596-b5c6-8f41d73d8dc5';
SELECT REG_PATCH('20230227-01'); 