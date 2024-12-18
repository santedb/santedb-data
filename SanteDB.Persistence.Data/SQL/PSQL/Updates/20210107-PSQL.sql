﻿/** 
 * <feature scope="SanteDB.Persistence.Data" id="20210107-01" name="Update:20210107-01"   invariantName="npgsql">
 *	<summary>Update: Updates the authentication functions to fix bugs related to locking</summary>
 *	<isInstalled>select ck_patch('20210107-01')</isInstalled>
 * </feature>
 */

BEGIN TRANSACTION ;

 -- RETURNS WHETHER THE USER ACCOUNT IS LOCKED
CREATE OR REPLACE FUNCTION IS_USR_LOCK(
	USR_NAME_IN IN TEXT
) RETURNS BOOLEAN AS 
$$
BEGIN
	RETURN (SELECT (LOCKED > CURRENT_TIMESTAMP) FROM SEC_USR_TBL WHERE LOWER(USR_NAME) = LOWER(USR_NAME_IN));
END
$$ LANGUAGE PLPGSQL;

ALTER TABLE PHON_VAL_TBL ALTER COLUMN VAL TYPE VARCHAR(256);
INSERT INTO CD_SET_MEM_ASSOC_TBL (SET_ID, CD_ID) VALUES ('4E6DA567-0094-4F23-8555-11DA499593AF','ACAFE0F2-E209-43BB-8633-3665FD7C90BA');
-- ADD BIRTHPLACE
INSERT INTO ent_rel_vrfy_cdtbl (rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id, err_desc) VALUES ('f3ef7e48-d8b7-4030-b431-aff7e0e1cb76','bacd9c6f-3fa9-481e-9636-37457962804d','ACAFE0F2-E209-43BB-8633-3665FD7C90BA', 'Patient==[Birthplace]==>Precinct');
INSERT INTO ent_rel_vrfy_cdtbl (rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id, err_desc) VALUES ('bfcbb345-86db-43ba-b47e-e7411276ac7c','ACAFE0F2-E209-43BB-8633-3665FD7C90BA','79dd4f75-68e8-4722-a7f5-8bc2e08f5cd6', 'Precinct==[Parent]==>CityOrTown');
-- AUTHENTICATES THE USER IF APPLICABLE
CREATE OR REPLACE FUNCTION AUTH_USR (
	USR_NAME_IN IN TEXT,
	PASSWD_IN IN TEXT,
	MAX_FAIL_LOGIN_IN IN INT
) RETURNS TABLE (
    USR_ID UUID,
    CLS_ID UUID,
    USR_NAME VARCHAR(64),
    EMAIL VARCHAR(256),
    EMAIL_CNF BOOLEAN,
    PHN_NUM VARCHAR(128), 
    PHN_CNF BOOLEAN,
    TFA_ENABLED BOOLEAN,
    LOCKED TIMESTAMPTZ, -- TRUE IF THE ACCOUNT HAS BEEN LOCKED
    PASSWD VARCHAR(128),
    SEC_STMP VARCHAR(128),
    FAIL_LOGIN INT,
    LAST_LOGIN_UTC TIMESTAMPTZ,
    CRT_UTC TIMESTAMPTZ,
    CRT_PROV_ID UUID, 
    OBSLT_UTC TIMESTAMPTZ,
    OBSLT_PROV_ID UUID, 
    UPD_UTC TIMESTAMPTZ,
    UPD_PROV_ID UUID, 
	PWD_EXP_UTC DATE, 
	TFA_MECH UUID,
    ERR_CODE VARCHAR(128)
) AS $$
DECLARE
	USR_TPL SEC_USR_TBL;
BEGIN
	SELECT INTO USR_TPL * 
		FROM SEC_USR_TBL
		WHERE LOWER(SEC_USR_TBL.USR_NAME) = LOWER(USR_NAME_IN)
		AND SEC_USR_TBL.OBSLT_UTC IS NULL;

	IF (IS_USR_LOCK(USR_NAME_IN)) THEN
		IF(USR_TPL.LOCKED < '9000-01-01' AND USR_TPL.FAIL_LOGIN > MAX_FAIL_LOGIN_IN) THEN
			USR_TPL.LOCKED = COALESCE(USR_TPL.LOCKED, CURRENT_TIMESTAMP) + ((USR_TPL.FAIL_LOGIN - MAX_FAIL_LOGIN_IN) ^ 1.5 * '30 SECONDS'::INTERVAL);
		END IF;
		UPDATE SEC_USR_TBL SET FAIL_LOGIN = SEC_USR_TBL.FAIL_LOGIN + 1, LOCKED = USR_TPL.LOCKED
			WHERE LOWER(SEC_USR_TBL.USR_NAME) = LOWER(USR_NAME_IN);
		RETURN QUERY SELECT USR_TPL.*, ('AUTH_LCK:' || ((USR_TPL.LOCKED - CURRENT_TIMESTAMP)::TEXT))::VARCHAR;
	ELSE
		
		-- LOCKOUT ACCOUNTS
		IF(USR_TPL.FAIL_LOGIN > MAX_FAIL_LOGIN_IN) THEN 
			USR_TPL.LOCKED = COALESCE(USR_TPL.LOCKED, CURRENT_TIMESTAMP) + ((USR_TPL.FAIL_LOGIN - MAX_FAIL_LOGIN_IN) ^ 1.5 * '30 SECONDS'::INTERVAL);
			UPDATE SEC_USR_TBL SET FAIL_LOGIN = COALESCE(SEC_USR_TBL.FAIL_LOGIN, 0) + 1, LOCKED = USR_TPL.LOCKED
				WHERE LOWER(SEC_USR_TBL.USR_NAME) = LOWER(USR_NAME_IN);
			RETURN QUERY SELECT USR_TPL.*, ('AUTH_LCK:' || ((USR_TPL.LOCKED - CURRENT_TIMESTAMP)::TEXT))::VARCHAR;
		ELSIF (USR_TPL.PASSWD = PASSWD_IN) THEN
			UPDATE SEC_USR_TBL SET 
				FAIL_LOGIN = 0,
				LAST_LOGIN_UTC = CURRENT_TIMESTAMP,
				UPD_PROV_ID = 'fadca076-3690-4a6e-af9e-f1cd68e8c7e8',
				UPD_UTC = CURRENT_TIMESTAMP
			WHERE LOWER(SEC_USR_TBL.USR_NAME) = LOWER(USR_NAME_IN);
			RETURN QUERY SELECT USR_TPL.*, NULL::VARCHAR LIMIT 1;
		ELSE
			UPDATE SEC_USR_TBL SET FAIL_LOGIN = COALESCE(SEC_USR_TBL.FAIL_LOGIN, 0) + 1 WHERE LOWER(SEC_USR_TBL.USR_NAME) = LOWER(USR_NAME_IN);
			RETURN QUERY SELECT USR_TPL.*, ('AUTH_INV:' || USR_NAME_IN)::VARCHAR;
		END IF;
	END IF;
END	
$$ LANGUAGE PLPGSQL;

SELECT REG_PATCH('20210107-01');
COMMIT;