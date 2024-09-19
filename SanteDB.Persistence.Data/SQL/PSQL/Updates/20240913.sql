/** 
 * <feature scope="SanteDB.Persistence.Data" id="20240913-01" name="Update:20240913-01" invariantName="npgsql">
 *	<summary>Update: Registers the care pathway table</summary>
 *	<isInstalled>select ck_patch('20240913-01')</isInstalled>
 * </feature>
 */

 ALTER TABLE cp_tbl DROP COLUMN prog;
 CREATE TABLE cp_def_tbl (
	pth_id UUID NOT NULL DEFAULT uuid_generate_v1(), 
	mnemonic VARCHAR(128) NOT NULL UNIQUE, 
	descr TEXT,
	enrol INT,
	elig TEXT,
	tpl_id UUID, 
	crt_prov_id UUID NOT NULL,
	crt_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
	upd_prov_id UUID,
	upd_utc TIMESTAMPTZ,
	obslt_prov_id UUID,
	obslt_utc TIMESTAMPTZ,
	CONSTRAINT pk_cp_def_tbl PRIMARY KEY (pth_id),
	CONSTRAINT fk_cp_def_tpl_id FOREIGN KEY (tpl_id) REFERENCES tpl_def_tbl(tpl_id),
	CONSTRAINT fk_cp_def_crt_prov_id FOREIGN KEY (crt_prov_id) REFERENCES sec_prov_tbl(prov_id),
	CONSTRAINT fk_cp_def_upd_prov_id FOREIGN KEY (upd_prov_id) REFERENCES sec_prov_tbl(prov_id),
	CONSTRAINT fk_cp_def_obslt_prov_id FOREIGN KEY (obslt_prov_id) REFERENCES sec_prov_tbl(prov_id)
 );

 ALTER TABLE cp_tbl ADD pth_id UUID;
 ALTER TABLE cp_tbl ADD CONSTRAINT fk_cp_pth_def_tbl FOREIGN KEY (pth_id) REFERENCES cp_def_tbl(pth_id);
  
 SELECT REG_PATCH('20240913-01'); 
