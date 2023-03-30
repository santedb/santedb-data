/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230330-01" name="Update:20230330-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="npgsql">
 *	<summary>Update: Adds concept set references to the database</summary>
 *	<isInstalled>select ck_patch('20230330-01')</isInstalled>
 * </feature>
 */
 
 CREATE TABLE cd_set_comp_assoc_tbl (
	SET_COMP_ID UUID NOT NULL DEFAULT uuid_generate_v1(),
	SET_ID UUID NOT NULL,
	TRG_SET_ID UUID NOT NULL,
	ROL_CS INT NOT NULL CHECK(ROL_CS IN (1,2)),
	CONSTRAINT PK_CD_SET_COMP_ASSOC_TBL PRIMARY KEY (SET_COMP_ID),
	CONSTRAINT FK_CD_SET_COMP_ASSOC_SRC_SET_TBL FOREIGN KEY (SET_ID) REFERENCES CD_SET_TBL(SET_ID),
	CONSTRAINT FK_CD_SET_COMP_ASSOC_TRG_SET_TBL FOREIGN KEY (TRG_SET_ID) REFERENCES CD_SET_TBL(SET_ID)
);

CREATE OR REPLACE FUNCTION is_cd_set_mem(cd_id_in uuid, set_id_in uuid)
 RETURNS boolean
AS $$
BEGIN
 RETURN EXISTS (SELECT 1 
 		FROM CD_SET_MEM_ASSOC_TBL 
		WHERE
	 		cd_id_in = cd_id AND
			set_id = set_id_in or 
			(set_id in (select trg_set_id from cd_set_comp_assoc_tbl where set_id = set_id_in and rol_cs = 1) and
			set_id not in (select trg_set_id from cd_set_comp_assoc_tbl where set_id = set_id_in and rol_cs = 2))
		);
END; $$ LANGUAGE PLPGSQL;

CREATE OR REPLACE FUNCTION public.is_cd_set_mem(cd_id_in uuid, set_mnemonic_in character varying)
 RETURNS boolean
 LANGUAGE plpgsql
AS $function$
BEGIN
	RETURN EXISTS 
		(SELECT 1
			FROM  
				cd_set_mem_assoc_tbl csmt
				LEFT JOIN cd_set_comp_assoc_tbl csct ON (csct.trg_set_id = csmt.set_id)
				INNER JOIN cd_set_tbl cst ON (cst.set_id = csct.set_id OR cst.set_id = csmt.set_id)
			WHERE 
				(cst.mnemonic = set_mnemonic_in AND COALESCE(csct.rol_cs, 1) = 1
					OR csmt.set_id = '51925f69-14a3-4516-bb2c-e2280a4f3065')
				AND cd_id = cd_id_in);
END;
$function$
;

SELECT REG_PATCH('20230330-01'); 