﻿/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230118-01" name="Update:20230118-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="npgsql">
 *	<summary>Update: Migrate Nationality and VIP to person</summary>
 *	<isInstalled>select ck_patch('20230118-01')</isInstalled>
 * </feature>
 */
ALTER TABLE PSN_TBL ADD VIP_STS_CD_ID UUID;
ALTER TABLE PSN_TBL ADD CONSTRAINT CK_VIP_STS_CD CHECK (((VIP_STS_CD_ID IS NULL) OR IS_CD_SET_MEM(VIP_STS_CD_ID, 'VeryImportantPersonStatus') OR IS_CD_SET_MEM(VIP_STS_CD_ID, 'NullReason')));
ALTER TABLE PSN_TBL ADD NAT_CD_ID UUID;
ALTER TABLE PSN_TBL ADD DCSD_UTC DATE;
ALTER TABLE PSN_TBL ADD DCSD_PREC CHAR(1);
ALTER TABLE PSN_TBL ADD CONSTRAINT CK_DCSD_PREC CHECK (DCSD_PREC IN ('Y','M','D'));

UPDATE ENT_VRSN_TBL SET CLS_CD_ID = '4d1a5c28-deb7-411e-b75f-d524f90dfa63' WHERE CLS_CD_ID = '8CF4B0B0-84E5-4122-85FE-6AFA8240C218';
UPDATE PSN_TBL SET 
	VIP_STS_CD_ID = PAT_TBL.VIP_STS_CD_ID , 
	NAT_CD_ID = PAT_TBL.NAT_CD_ID,
	DCSD_UTC = PAT_TBL.DCSD_UTC,
	DCSD_PREC = PAT_TBL.DCSD_PREC
	FROM PAT_TBL WHERE PSN_TBL.ENT_VRSN_ID = PAT_TBL.ENT_VRSN_ID ; 

ALTER TABLE PAT_TBL DROP VIP_STS_CD_ID;
ALTER TABLE PAT_TBL DROP NAT_CD_ID;
ALTER TABLE PAT_TBL DROP DCSD_PREC CASCADE;
ALTER TABLE PAT_TBL DROP DCSD_UTC CASCADE;

UPDATE CD_VRSN_TBL SET MNEMONIC = 'DataEnterer' WHERE CD_ID = 'C50D66D2-E5DA-4A34-B2B7-4CD4FE4EF2C4';--#!

CREATE OR REPLACE FUNCTION trg_vrfy_ent_rel_tbl()
 RETURNS trigger
AS $$
BEGIN
	IF (NEW.obslt_vrsn_seq_id IS NULL AND NOT EXISTS(
		SELECT 1 
		FROM 
			rel_vrfy_systbl
		WHERE 
			EXISTS(SELECT 1 FROM ent_vrsn_tbl WHERE ent_id = NEW.src_ent_id AND head AND (cls_cd_id = src_cls_cd_id OR src_cls_cd_id IS NULL))
			AND EXISTS(SELECT 1 FROM ent_vrsn_tbl WHERE ent_id = NEW.trg_ent_id AND head AND (cls_cd_id = trg_cls_cd_id OR trg_cls_cd_id IS NULL))
			AND rel_typ_cd_id = NEW.rel_typ_cd_id
			AND rel_cls = 1
	)) THEN 
		RAISE EXCEPTION 'Validation error: Relationship %  between % > % is invalid', NEW.rel_typ_cd_id, NEW.src_ent_id, NEW.trg_ent_id
			USING ERRCODE = 'O9001';
	END IF;
	RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION trg_vrfy_act_rel_tbl()
 RETURNS trigger
AS $$
BEGIN
	IF (NEW.obslt_vrsn_seq_id IS NULL AND NOT EXISTS(
		SELECT 1 
		FROM 
			rel_vrfy_systbl
		WHERE 
			EXISTS(SELECT 1 FROM act_vrsn_tbl WHERE act_id = NEW.src_act_id AND head AND (cls_cd_id = src_cls_cd_id OR src_cls_cd_id IS NULL))
			AND EXISTS(SELECT 1 FROM act_vrsn_tbl WHERE act_id = NEW.trg_act_id AND head AND (cls_cd_id = trg_cls_cd_id OR trg_cls_cd_id IS NULL))
			AND rel_typ_cd_id = NEW.rel_typ_cd_id
			AND rel_cls = 2
	)) THEN 
		RAISE EXCEPTION 'Validation error: Relationship %  between % > % is invalid', NEW.rel_typ_cd_id, NEW.src_act_id, NEW.trg_act_id
			USING ERRCODE = 'O9001';
	END IF;
	RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION trg_vrfy_act_ptcpt_tbl()
 RETURNS trigger
AS $$
BEGIN
	IF (NEW.obslt_vrsn_seq_id IS NULL AND NOT EXISTS(
		SELECT 1 
		FROM 
			rel_vrfy_systbl
		WHERE 
			EXISTS(SELECT 1 FROM act_vrsn_tbl WHERE act_id = NEW.act_id AND head AND (cls_cd_id = src_cls_cd_id OR src_cls_cd_id IS NULL))
			AND EXISTS(SELECT 1 FROM ent_vrsn_tbl WHERE ent_id = NEW.ent_id AND head AND (cls_cd_id = trg_cls_cd_id OR trg_cls_cd_id IS NULL))
			AND rel_typ_cd_id = NEW.rol_cd_id
			AND rel_cls = 3
	)) THEN 
		RAISE EXCEPTION 'Validation error: Relationship %  between % > % is invalid', NEW.rol_cd_id, NEW.act_id, NEW.ent_id
			USING ERRCODE = 'O9001';
	END IF;
	RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create a function that always returns the first non-NULL item
CREATE OR REPLACE FUNCTION public.first_nvl_agg ( anyelement, anyelement )
RETURNS anyelement AS $$
        SELECT COALESCE($1, $2);
$$ LANGUAGE SQL IMMUTABLE STRICT ;

-- And then wrap an aggregate around it
CREATE AGGREGATE public.FIRST_NVL (
        sfunc    = public.first_nvl_agg,
        basetype = anyelement,
        stype    = anyelement
);
 
SELECT REG_PATCH('20230118-01'); 