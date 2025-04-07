/** 
 * <feature scope="SanteDB.Persistence.Data" id="20250407-01" name="Update:20250407-01"   invariantName="npgsql" >
 *	<summary>Update: Add notification and notification template tables</summary>
 *	<isInstalled>select ck_patch('20250407-01')</isInstalled>
 * </feature>
 */

CREATE TABLE nfn_tpl_tbl (
    tpl_id UUID PRIMARY KEY DEFAULT uuid_generate_v1(),
    sts_cd_id UUID NOT NULL,
    mnemonic VARCHAR(128) NOT NULL,
    tpl_name VARCHAR(128) NOT NULL,
    tags VARCHAR(128) NULL,
    crt_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    crt_prov_id UUID NOT NULL,
    upd_utc TIMESTAMPTZ NULL,
    upd_prov_id UUID NULL,
    obslt_utc TIMESTAMPTZ NULL,
    obslt_prov_id UUID NULL,
    CONSTRAINT fk_crt_id_tpl_sec_usr_id FOREIGN KEY (crt_prov_id) REFERENCES sec_prov_tbl(prov_id),
    CONSTRAINT fk_upd_id_tpl_sec_usr_id FOREIGN KEY (upd_prov_id) REFERENCES sec_prov_tbl(prov_id),
    CONSTRAINT fk_obslt_id_tpl_sec_usr_id FOREIGN KEY (obslt_prov_id) REFERENCES sec_prov_tbl(prov_id),
    CONSTRAINT fk_sts_cd_id_cd_tbl FOREIGN KEY (sts_cd_id) REFERENCES cd_tbl(cd_id),
    CONSTRAINT ck_upd_id_upd_utc CHECK ((upd_prov_id IS NULL AND upd_utc IS NULL) OR (upd_prov_id IS NOT NULL AND upd_utc IS NOT NULL)),
    CONSTRAINT ck_obslt_id_obslt_utc CHECK ((obslt_prov_id IS NULL AND obslt_utc IS NULL) OR (obslt_prov_id IS NOT NULL AND obslt_utc IS NOT NULL)),
    CONSTRAINT ck_tpl_name_unique UNIQUE (tpl_name),
    CONSTRAINT ck_tpl_mnemonic_unique UNIQUE (mnemonic)
);

CREATE TABLE nfn_tpl_cnt_tbl (
    tpl_cnt_id UUID PRIMARY KEY DEFAULT uuid_generate_v1(),
    nfn_tpl_id UUID NULL,
    lang_cs VARCHAR(2) NULL,
    sbj TEXT NULL,
    bdy TEXT NOT NULL,
    CONSTRAINT fk_tpl_id_tpl_tbl FOREIGN KEY (nfn_tpl_id) REFERENCES nfn_tpl_tbl(tpl_id)
);

CREATE TABLE nfn_tpl_prm_tbl (
    tpl_prm_id UUID PRIMARY KEY DEFAULT uuid_generate_v1(),
    nfn_tpl_id UUID NULL,
    prm_name VARCHAR(128) NOT NULL,
    descr TEXT NULL,
    CONSTRAINT fk_nfn_tpl_id_nfn_tpl_tbl FOREIGN KEY (nfn_tpl_id) REFERENCES nfn_tpl_tbl(tpl_id)
);

CREATE TABLE nfn_inst_tbl (
    inst_id UUID PRIMARY KEY DEFAULT uuid_generate_v1(),
    nfn_tpl_id UUID NOT NULL,
    ent_typ_cd_id UUID NOT NULL,
    mnemonic VARCHAR(128) NOT NULL,
    inst_name VARCHAR(128) NOT NULL,
    state_cd_id UUID NOT NULL,
    descr TEXT NULL,
    fltr_expr TEXT NOT NULL,
    trgr_expr TEXT NOT NULL,
    trg_expr TEXT NOT NULL,
    last_sent_utc TIMESTAMPTZ NULL,
    crt_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    crt_prov_id UUID NOT NULL,
    upd_utc TIMESTAMPTZ NULL,
    upd_prov_id UUID NULL,
    obslt_utc TIMESTAMPTZ NULL,
    obslt_prov_id UUID NULL,
    CONSTRAINT fk_crt_id_nfn_inst_sec_prov_id FOREIGN KEY (crt_prov_id) REFERENCES sec_prov_tbl(prov_id),
    CONSTRAINT fk_upd_id_nfn_inst_sec_prov_id FOREIGN KEY (upd_prov_id) REFERENCES sec_prov_tbl(prov_id),
    CONSTRAINT fk_obslt_id_nfn_inst_sec_prov_id FOREIGN KEY (obslt_prov_id) REFERENCES sec_prov_tbl(prov_id),
    CONSTRAINT fk_state_cd_id_cd_tbl FOREIGN KEY (state_cd_id) REFERENCES cd_tbl(cd_id),
    CONSTRAINT fk_nfn_tpl_id_cd_tbl FOREIGN KEY (nfn_tpl_id) REFERENCES nfn_tpl_tbl(tpl_id),
    CONSTRAINT ck_upd_id_upd_utc CHECK ((upd_prov_id IS NULL AND upd_utc IS NULL) OR (upd_prov_id IS NOT NULL AND upd_utc IS NOT NULL)),
    CONSTRAINT ck_obslt_id_obslt_utc CHECK ((obslt_prov_id IS NULL AND obslt_utc IS NULL) OR (obslt_prov_id IS NOT NULL AND obslt_utc IS NOT NULL)),
    CONSTRAINT ck_nfn_inst_name_unique UNIQUE (inst_name),
    CONSTRAINT ck_nfn_inst_mnemonic_unique UNIQUE (mnemonic)
);

CREATE TABLE nfn_inst_prm_tbl (
    prm_val_id UUID PRIMARY KEY DEFAULT uuid_generate_v1(),
    nfn_inst_id UUID NOT NULL,
    nfn_tpl_prm_id UUID NOT NULL,
    expr TEXT NOT NULL,
    CONSTRAINT fk_nfn_inst_id_nfn_inst_tbl FOREIGN KEY (nfn_inst_id) REFERENCES nfn_inst_tbl(inst_id),
    CONSTRAINT fk_nfn_tpl_prm_id_nfn_tpl_prm_tbl FOREIGN KEY (nfn_tpl_prm_id) REFERENCES nfn_inst_tbl(tpl_prm_id)
);

SELECT REG_PATCH('20250407-01');