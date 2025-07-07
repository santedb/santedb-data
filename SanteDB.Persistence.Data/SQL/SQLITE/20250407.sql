/** 
 * <feature scope="SanteDB.Persistence.Data" id="20250407-01" name="Update:20250407-01"  invariantName="sqlite">
 *	<summary>Update: AAdd notification and notification template tables</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE name='nfn_tpl_tbl' OR name='nfn_tpl_cnt_tbl' OR name='nfn_tpl_prm_tbl'OR name='nfn_inst_tbl'OR name='nfn_inst_prm_tbl')</isInstalled>
 * </feature>
 */
CREATE TABLE nfn_tpl_tbl (
    tpl_id blob(16) DEFAULT (randomblob(16)) NOT NULL,
    sts_cd_id blob(16) NOT NULL,
    mnemonic VARCHAR(128) NOT NULL,
    tpl_name VARCHAR(128) NOT NULL,
    tags VARCHAR(128) NULL,
    crt_utc BIGINT NOT NULL DEFAULT (unixepoch()),
    crt_prov_id blob(16) NOT NULL,
    upd_utc BIGINT NULL,
    upd_prov_id blob(16) NULL,
    obslt_utc BIGINT NULL,
    obslt_prov_id blob(16) NULL,
    CONSTRAINT fk_crt_id_tpl_sec_usr_id FOREIGN KEY (crt_prov_id) REFERENCES sec_prov_tbl(prov_id),
    CONSTRAINT fk_upd_id_tpl_sec_usr_id FOREIGN KEY (upd_prov_id) REFERENCES sec_prov_tbl(prov_id),
    CONSTRAINT fk_obslt_id_tpl_sec_usr_id FOREIGN KEY (obslt_prov_id) REFERENCES sec_prov_tbl(prov_id),
    CONSTRAINT fk_sts_cd_id_cd_tbl FOREIGN KEY (sts_cd_id) REFERENCES cd_tbl(cd_id),
    CONSTRAINT ck_upd_id_upd_utc CHECK (
        (
            upd_prov_id IS NULL
            AND upd_utc IS NULL
        )
        OR (
            upd_prov_id IS NOT NULL
            AND upd_utc IS NOT NULL
        )
    ),
    CONSTRAINT ck_obslt_id_obslt_utc CHECK (
        (
            obslt_prov_id IS NULL
            AND obslt_utc IS NULL
        )
        OR (
            obslt_prov_id IS NOT NULL
            AND obslt_utc IS NOT NULL
        )
    ),
    CONSTRAINT ck_tpl_name_unique UNIQUE (tpl_name),
    CONSTRAINT ck_tpl_mnemonic_unique UNIQUE (mnemonic)
);
CREATE TABLE nfn_tpl_cnt_tbl (
    tpl_cnt_id blob(16) DEFAULT (randomblob(16)) NOT NULL,
    nfn_tpl_id blob(16) NULL,
    lang_cs VARCHAR(2) NULL,
    sbj TEXT NULL,
    bdy TEXT NOT NULL,
    CONSTRAINT fk_tpl_id_tpl_tbl FOREIGN KEY (nfn_tpl_id) REFERENCES nfn_tpl_tbl(tpl_id)
);
CREATE TABLE nfn_tpl_prm_tbl (
    tpl_prm_id blob(16) DEFAULT (randomblob(16)) NOT NULL,
    nfn_tpl_id blob(16) NULL,
    prm_name VARCHAR(128) NOT NULL,
    descr TEXT NULL,
    CONSTRAINT fk_nfn_tpl_id_nfn_tpl_tbl FOREIGN KEY (nfn_tpl_id) REFERENCES nfn_tpl_tbl(tpl_id)
);
CREATE TABLE nfn_inst_tbl (
    inst_id blob(16) DEFAULT (randomblob(16)) NOT NULL,
    nfn_tpl_id blob(16) NOT NULL,
    ent_typ_cd_id blob(16) NOT NULL,
    mnemonic VARCHAR(128) NOT NULL,
    inst_name VARCHAR(128) NOT NULL,
    state_cd_id blob(16) NOT NULL,
    descr TEXT NULL,
    fltr_expr TEXT NOT NULL,
    trgr_expr TEXT NOT NULL,
    trg_expr TEXT NOT NULL,
    last_sent_utc BIGINT NULL,
    crt_utc BIGINT NOT NULL DEFAULT (unixepoch()),
    crt_prov_id blob(16) NOT NULL,
    upd_utc BIGINT NULL,
    upd_prov_id blob(16) NULL,
    obslt_utc BIGINT NULL,
    obslt_prov_id blob(16) NULL,
    CONSTRAINT fk_crt_id_nfn_inst_sec_prov_id FOREIGN KEY (crt_prov_id) REFERENCES sec_prov_tbl(prov_id),
    CONSTRAINT fk_upd_id_nfn_inst_sec_prov_id FOREIGN KEY (upd_prov_id) REFERENCES sec_prov_tbl(prov_id),
    CONSTRAINT fk_obslt_id_nfn_inst_sec_prov_id FOREIGN KEY (obslt_prov_id) REFERENCES sec_prov_tbl(prov_id),
    CONSTRAINT fk_state_cd_id_cd_tbl FOREIGN KEY (state_cd_id) REFERENCES cd_tbl(cd_id),
    CONSTRAINT fk_nfn_tpl_id_cd_tbl FOREIGN KEY (nfn_tpl_id) REFERENCES nfn_tpl_tbl(tpl_id),
    CONSTRAINT ck_upd_id_upd_utc CHECK ((upd_prov_id IS NULL AND upd_utc IS NULL) OR ( upd_prov_id IS NOT NULL AND upd_utc IS NOT NULL)),
    CONSTRAINT ck_obslt_id_obslt_utc CHECK ((obslt_prov_id IS NULL AND obslt_utc IS NULL) OR (obslt_prov_id IS NOT NULL AND obslt_utc IS NOT NULL)),
    CONSTRAINT ck_nfn_inst_name_unique UNIQUE (inst_name),
    CONSTRAINT ck_nfn_inst_mnemonic_unique UNIQUE (mnemonic)
);
CREATE TABLE nfn_inst_prm_tbl (
    prm_val_id blob(16) DEFAULT (randomblob(16)) NOT NULL,
    nfn_inst_id blob(16) NOT NULL,
    nfn_tpl_prm_name VARCHAR(128) NOT NULL,
    expr TEXT NOT NULL,
    CONSTRAINT fk_nfn_inst_id_nfn_inst_tbl FOREIGN KEY (nfn_inst_id) REFERENCES nfn_inst_tbl(inst_id)
);