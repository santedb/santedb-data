/** 
 * <feature scope="SanteDB.Persistence.Data" id="20241103-01" name="Update:20241103-01"   invariantName="npgsql"  environment="Server">
 *	<summary>Update: Migrates the MDM ownership to a creation act which holds the metadata information for the ownership for each MDM update</summary>
 *	<isInstalled>select ck_patch('20241103-01')</isInstalled>
 * </feature>
 */

CREATE VIEW MDM_TRANSITION_CACT_VW AS
	SELECT ENT_ID, CRT_PROV_ID, CRT_UTC FROM 
		ent_vrsn_tbl 
	WHERE 
		cls_cd_id = 'bacd9c6f-3fa9-481e-9636-37457962804d'
		AND ent_vrsn_tbl.rplc_vrsn_id IS NULL
		AND EXISTS (
			SELECT 1 
			FROM ent_rel_tbl 
			WHERE 
				src_ent_id = ent_vrsn_tbl.ent_id 
				AND rel_typ_cd_id = '97730a52-7e30-4dcd-94cd-fd532d111578'
		) 
		AND ent_vrsn_tbl.CRT_ACT_ID IS NULL;--#!
	

INSERT INTO ACT_TBL (ACT_ID) 
	SELECT ENT_ID FROM MDM_TRANSITION_CACT_VW; --#!

-- INFO: Creating CACT Entries for MDM Patients (This can take a few hours)
INSERT INTO ACT_VRSN_TBL (ACT_ID, CRT_PROV_ID, ACT_UTC, TYP_CD_ID, CLS_CD_ID, MOD_CD_ID, STS_CD_ID, HEAD)
	SELECT ENT_ID, CRT_PROV_ID, CRT_UTC, 'D34081D8-740A-48E2-9E4B-E418CFE174FF', 'd874424e-c692-4fd8-b94e-642e1cbf83e9', 'EC74541F-87C4-4327-A4B9-97F325501747', 'AFC33800-8225-4061-B168-BACC09CDBAE3', TRUE
	FROM MDM_TRANSITION_CACT_VW; --#!

-- INFO: Updating Entity Creation Acts 
UPDATE ENT_VRSN_TBL SET CRT_ACT_ID = ENT_ID 
WHERE
	ENT_ID IN (SELECT ENT_ID FROM MDM_TRANSITION_CACT_VW);
 --#!
DROP VIEW MDM_TRANSITION_CACT_VW;--#!
SELECT REG_PATCH('20241103-01');