/** 
 * <feature scope="SanteDB.Persistence.Data" id="20241103" name="Update:20241103"  invariantName="sqlite" environment="Server">
 *	<summary>Migrates the MDM ownership to a creation act which holds the metadata information for the ownership for each MDM update</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM patch_db_systbl WHERE patch_id='20241103')</isInstalled>
 * </feature>
 */
CREATE VIEW MDM_TRANSITION_CACT_VW AS
	SELECT ENT_ID, CRT_PROV_ID, CRT_UTC FROM 
		ent_vrsn_tbl 
	WHERE 
		cls_cd_id = x'6F9CCDBAA93F1E48963637457962804D'
		AND ent_vrsn_tbl.rplc_vrsn_id IS NULL
		AND EXISTS (
			SELECT 1 
			FROM ent_rel_tbl 
			WHERE 
				src_ent_id = ent_vrsn_tbl.ent_id 
				AND rel_typ_cd_id = x'520A7397307ECD4D94CDFD532D111578'
		) 
		AND ent_vrsn_tbl.CRT_ACT_ID IS NULL;
--#!	

INSERT INTO ACT_TBL (ACT_ID) 
	SELECT ENT_ID FROM MDM_TRANSITION_CACT_VW;
--#!

INSERT INTO ACT_VRSN_TBL (ACT_ID, CRT_PROV_ID, ACT_UTC, TYP_CD_ID, CLS_CD_ID, MOD_CD_ID, STS_CD_ID, HEAD)
	SELECT ENT_ID, CRT_PROV_ID, CRT_UTC, x'D88140D30A74E2489E4BE418CFE174FF', x'4E4274D892C6D84FB94E642E1CBF83E9', x'1F5474ECC4872743A4B997F325501747', x'0038C3AF25826140B168BACC09CDBAE3', 1
	FROM MDM_TRANSITION_CACT_VW;
--#!
UPDATE ENT_VRSN_TBL SET CRT_ACT_ID = ENT_ID 
WHERE
	ENT_ID IN (SELECT ENT_ID FROM MDM_TRANSITION_CACT_VW);
--#!
DROP VIEW MDM_TRANSITION_CACT_VW;--#!
INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20241103', unixepoch(), 'Migrates the MDM ownership to a creation act which holds the metadata ');--#!