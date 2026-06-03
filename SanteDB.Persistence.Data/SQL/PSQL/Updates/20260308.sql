/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260308-02" name="Update:20260308-02"   invariantName="npgsql" >
 *	<summary>Update: Refactor the mail message tables</summary>
 *	<isInstalled>select ck_patch('20260308-02')</isInstalled>
 * </feature>
 */
 
DROP TABLE mail_box_msg_assoc_tbl CASCADE; 
 DROP TABLE mail_box_tbl  CASCADE;
 DROP TABLE mail_msg_rcpt_to_tbl CASCADE;
 DROP TABLE mail_msg_tbl CASCADE;

 CREATE TABLE mail_msg_tbl (
	mail_msg_id UUID NOT NULL DEFAULT uuid_generate_v1(),
	crt_utc timestamptz DEFAULT CURRENT_TIMESTAMP NOT NULL,
	crt_prov_id uuid NOT NULL,
	obslt_utc timestamptz NULL,
	obslt_prov_id uuid NULL,
	frm UUID NOT NULL,
	frm_txt VARCHAR(256) NOT NULL,
	to_txt VARCHAR(1024) NOT NULL,
	flags INT NOT NULL DEFAULT 0,
	subj VARCHAR(256) NOT NULL,
	body TEXT NOT NULL,
	CONSTRAINT pk_mail_msg_tbl PRIMARY KEY (mail_msg_id),
	CONSTRAINT fk_mail_msg_crt_prov_id FOREIGN KEY (crt_prov_id) REFERENCES sec_prov_tbl(prov_id),
	CONSTRAINT fk_mail_msg_obslt_prov_id FOREIGN KEY (obslt_prov_id) REFERENCES sec_prov_tbl(prov_id),
	CONSTRAINT ck_mail_obslt_utc CHECK (obslt_utc IS NULL AND obslt_prov_id IS NULL OR obslt_utc IS NOT NULL AND obslt_prov_id IS NOT NULL)
);

CREATE TABLE mail_box_tbl (
	mail_box_id UUID NOT NULL DEFAULT uuid_generate_v1(),
	own_id UUID NOT NULL, 
	name VARCHAR(32) NOT NULL,
	crt_utc TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
	crt_prov_id UUID NOT NULL,
	obslt_utc TIMESTAMPTZ NULL,
	obslt_prov_id UUID NULL,
	CONSTRAINT pk_mail_box_tbl PRIMARY KEY (mail_box_id),
	CONSTRAINT fk_mail_box_crt_prov_id FOREIGN KEY (crt_prov_id) REFERENCES sec_prov_tbl(prov_id),
	CONSTRAINT fk_mail_box_obslt_prov_id FOREIGN KEY (obslt_prov_id) REFERENCES sec_prov_tbl(prov_id)
);

CREATE UNIQUE INDEX ix_mail_box_owner_name_uq ON mail_box_tbl(OWN_ID, NAME) WHERE (OBSLT_UTC IS NULL);
CREATE INDEX ix_mail_box_owner ON mail_box_tbl(OWN_ID);

CREATE TABLE mail_msg_rcpt_to_tbl (
	mail_msg_id UUID NOT NULL,
	rcpt_id UUID NOT NULL,
	CONSTRAINT pk_mail_msg_rcpt_to_tbl PRIMARY KEY (mail_msg_id, rcpt_id),
	CONSTRAINT fk_mail_msg_rcpt_to_mail_id FOREIGN KEY (mail_msg_id) REFERENCES mail_msg_tbl(mail_msg_id)
);

CREATE INDEX ix_mail_msg_rcpt_to_msg ON mail_msg_rcpt_to_tbl (mail_msg_id);

CREATE TABLE mail_box_msg_assoc_tbl (
	mail_box_msg_id UUID NOT NULL DEFAULT uuid_generate_v1(),
	mail_box_id UUID NOT NULL,
	mail_msg_id UUID NOT NULL,
	sts INT NOT NULL DEFAULT 0,
	msg_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
	crt_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
	CONSTRAINT pk_mail_box_msg_assoc_tbl PRIMARY KEY (mail_box_msg_id),
	CONSTRAINT fk_mail_box_msg_assoc_mail_box_tbl FOREIGN KEY (mail_box_id) REFERENCES mail_box_tbl(mail_box_id),
	CONSTRAINT fk_mail_box_msg_assoc_mail_msg_tbl FOREIGN KEY (mail_msg_id) REFERENCES mail_msg_tbl(mail_msg_id)
);

CREATE INDEX ix_mail_box_msg_assoc_mail_box ON mail_box_msg_assoc_tbl(mail_box_id);
CREATE UNIQUE INDEX uq_ix_mail_box_msg_assoc_mail_box ON mail_box_msg_assoc_tbl(mail_box_id, mail_msg_id);


 SELECT REG_PATCH('20260308-02');