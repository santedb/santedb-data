﻿/** 
 * <feature scope="SanteDB.Persistence.Data" id="20201214-01" name="Update:20201214-01"   invariantName="npgsql">
 *	<summary>Update: Add PURGED status key</summary>
 *	<remarks>Adds status key PURGED to the database</remarks>
 *	<isInstalled>select ck_patch('20201214-01')</isInstalled>
 * </feature>
 */
BEGIN TRANSACTION;

INSERT INTO CD_TBL (CD_ID) VALUES ('39995C08-0A5C-4549-8BA7-D187F9B3C4FD') ON CONFLICT DO NOTHING;
INSERT INTO CD_VRSN_TBL (CD_ID, STS_CD_ID, CLS_ID, CRT_PROV_ID, CRT_UTC, MNEMONIC) VALUES ('39995C08-0A5C-4549-8BA7-D187F9B3C4FD', 'C8064CBD-FA06-4530-B430-1A52F1530C27', '54B93182-FC19-47A2-82C6-089FD70A4F45', 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8', CURRENT_TIMESTAMP, 'PURGED') ON CONFLICT DO NOTHING;
INSERT INTO CD_NAME_TBL (CD_ID, EFFT_VRSN_SEQ_ID, LANG_CS, VAL) 
	SELECT '39995C08-0A5C-4549-8BA7-D187F9B3C4FD', vrsn_seq_id, 'EN', 'Purged'
	FROM CD_VRSN_TBL WHERE CD_ID = '39995C08-0A5C-4549-8BA7-D187F9B3C4FD' ON CONFLICT DO NOTHING;
INSERT INTO CD_SET_MEM_ASSOC_TBL  (CD_ID, SET_ID) VALUES ('39995C08-0A5C-4549-8BA7-D187F9B3C4FD', '93A48F6A-6808-4C70-83A2-D02178C2A883') ON CONFLICT DO NOTHING;
INSERT INTO CD_SET_MEM_ASSOC_TBL  (CD_ID, SET_ID) VALUES ('39995C08-0A5C-4549-8BA7-D187F9B3C4FD', 'AAE906AA-27B3-4CDB-AFF1-F08B0FD31E59') ON CONFLICT DO NOTHING;
INSERT INTO CD_SET_MEM_ASSOC_TBL  (CD_ID, SET_ID) VALUES ('39995C08-0A5C-4549-8BA7-D187F9B3C4FD', 'C7578340-A8FF-4D7D-8105-581016324E68') ON CONFLICT DO NOTHING;

ALTER TABLE pat_tbl ADD vip_sts_cd_id UUID;
ALTER TABLE pat_tbl ADD CONSTRAINT fk_vip_sts_cd_cd_id FOREIGN KEY (vip_sts_cd_id) REFERENCES CD_TBL (CD_ID);
ALTER TABLE pat_tbl ADD CONSTRAINT ck_vip_sts_cd CHECK (vip_sts_cd_id IS NULL OR IS_CD_SET_MEM(vip_sts_cd_id, 'VipStatus') OR IS_CD_SET_MEM(vip_sts_cd_id, 'NullReason'));
ALTER TABLE psn_tbl ADD occ_cd_id UUID;
ALTER TABLE psn_tbl ADD CONSTRAINT fk_occ_cd_cd_id FOREIGN KEY (occ_cd_id) REFERENCES CD_TBL (CD_ID);
ALTER TABLE psn_tbl ADD CONSTRAINT ck_occ_cd CHECK (occ_cd_id IS NULL OR IS_CD_SET_MEM(occ_cd_id, 'OccupationType') OR IS_CD_SET_MEM(occ_cd_id, 'NullReason'));

ALTER TABLE ACT_VRSN_TBL DROP CONSTRAINT ck_act_vrsn_act_utc;
ALTER TABLE ACT_VRSN_TBL ADD CONSTRAINT ck_act_vrsn_act_utc CHECK (sts_cd_id = '39995C08-0A5C-4549-8BA7-D187F9B3C4FD' OR ((act_utc IS NOT NULL) OR (act_start_utc IS NOT NULL) OR (act_stop_utc IS NOT NULL)));

SELECT REG_PATCH('20201214-01');

COMMIT;
