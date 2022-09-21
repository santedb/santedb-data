﻿/** 
 * <feature scope="SanteDB.Persistence.Audit.ADO" id="00010000-00" name="Initialize:001-01" invariantName="npgsql">
 *	<summary>Installs the core schema for SanteDB Audit Repository</summary>
 *	<remarks>This script installs the necessary core schema files for SanteDB</remarks>
 *  <isInstalled mustSucceed="true">SELECT to_regclass('public.aud_cd_tbl') IS NOT NULL;</isInstalled>
 * </feature>
 */

 CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

 -- TABLE FOR STORAGE OF AUDIT CODES
 CREATE TABLE aud_cd_tbl (
	id	UUID NOT NULL DEFAULT uuid_generate_v4(), -- UNIQUE IDENTIIFER OF THE CODE
	mnemonic VARCHAR(256) NOT NULL, -- THE MNEMONIC OF THE CODE
	cd_sys VARCHAR(256), -- THE CODIFICATION SYSTEM OF THE CODE IF KNOWN
	CONSTRAINT pk_aud_cd_tbl PRIMARY KEY (id)
 );

 CREATE UNIQUE INDEX aud_cd_mnemonic_cd_sys_idx ON aud_cd_tbl(mnemonic, cd_sys);

 -- TABLE FOR STORAGE OF AUDITS
 CREATE TABLE aud_tbl (
	id UUID NOT NULL DEFAULT uuid_generate_v4(), -- UNIQUE IDENTIIFER FOR THE AUDIT
	outc_cs INT NOT NULL, -- THE OUTCOME OF THE AUDIT
	act_cs INT NOT NULL, -- THE ACTION CODE OF THE AUDIT
	typ_cs INT NOT NULL, -- THE AUDIT TYPE CODE
	evt_utc TIMESTAMPTZ NOT NULL, -- THE TIME THAT THE AUDIT OCCURRED
	crt_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP, -- THE TIME THAT THE AUDIT WAS CREATED
	cls_cd_id UUID, -- THE CLASSIFICATION CODE
	CONSTRAINT pk_aud_tbl PRIMARY KEY (id),
	CONSTRAINT fk_aud_cls_cd_id FOREIGN KEY (cls_cd_id) REFERENCES aud_cd_tbl(id)
 );
 
 -- TABLE FOR STORAGE OF AUDIT OBJECTS
 CREATE TABLE aud_obj_tbl (
	id	UUID NOT NULL DEFAULT uuid_generate_v4(), -- UNIQUE IDENTIFIER FOR THE AUDIT OBJECT
	aud_id UUID NOT NULL, -- AUDIT TO WHICH THE OBJECT BELONGS
	obj_id VARCHAR(256), -- THE OBJECT IDENTIFIER
	obj_typ INT, -- THE TYPE OF OBJECT
	rol_cs INT, -- THE ROLE THE OBJECT PLAYS IN THE AUDIT
	lcycl_cs INT, -- THE LIFECYCLE OF THE OBJECT
	id_typ_cs INT, -- THE IDENTIFIER TYPE CODE
	qry_dat TEXT, -- ADDITIONAL QUERY DATA ASSIGNED TO THE OBJECT
	nam_dat TEXT, -- ADDITIONAL NAME DATA ASSIGNED TO THE OBJECT
	CONSTRAINT pk_aud_obj_tbl PRIMARY KEY (id),
	CONSTRAINT fk_aud_obj_aud_tbl FOREIGN KEY (aud_id) REFERENCES aud_tbl(id)
);

CREATE INDEX aud_obj_obj_id_idx ON aud_obj_tbl(obj_id);
CREATE INDEX aud_obj_aud_id_idx ON aud_obj_tbl(aud_id);

-- TABLE FOR AUDIT ACTORS
CREATE TABLE aud_act_tbl (
	id UUID NOT NULL DEFAULT uuid_generate_v4(), -- UNIQUE IDENTIFIER FOR THE AUDIT ACTOR ENTRY
	usr_id VARCHAR(256), -- USER IDENTIFIER AS KNOWN BY THE SYSTEM
	usr_name VARCHAR(256), -- USER NAME AS KNOWN BY THE SYSTEM
	rol_cd_id UUID, -- THE ROLE CODE OF THE ACTOR
	CONSTRAINT pk_aud_act_tbl PRIMARY KEY (id),
	CONSTRAINT fk_aud_act_rol_cd_id FOREIGN KEY (rol_cd_id) REFERENCES aud_cd_tbl(id)
);

-- ASSOCIATION TABLE BETWEEN AUDITS AND ACTORS
CREATE TABLE aud_act_assoc_tbl (
	id UUID NOT NULL DEFAULT uuid_generate_v4(), -- UNIQUE IDENTIFIER FOR THE AUDIT ACTOR
	aud_id UUID NOT NULL, -- THE AUDIT TO WHICH THE ACTOR ENTRY BELONGS
	act_id UUID NOT NULL, -- THE ACTOR TO WHICH THE ASOSCIATION BELONGS
	is_rqo BOOLEAN NOT NULL DEFAULT FALSE, -- TRUE IF THE USER IS THE REQUESTOR OF THE ACTION
	ap VARCHAR(256) , -- ACCESS POINT
	CONSTRAINT pk_aud_act_assoc_tbl PRIMARY KEY (id),
	CONSTRAINT fk_aud_act_assoc_act_id FOREIGN KEY (act_id) REFERENCES aud_act_tbl (id),
	CONSTRAINT fk_aud_act_assoc_aud_id FOREIGN KEY (aud_id) REFERENCES aud_tbl (id)
);

CREATE INDEX aud_act_assoc_aud_id_idx ON aud_act_assoc_tbl(aud_id);

-- METADATA TABLE
CREATE TABLE aud_meta_tbl (
	id UUID NOT NULL DEFAULT uuid_generate_v4(),
	aud_id UUID NOT NULL,
	attr INT NOT NULL,
	val TEXT NOT NULL,
	CONSTRAINT pk_aud_meta_tbl PRIMARY KEY (id),
	CONSTRAINT fk_aud_meta_aud_id FOREIGN KEY (aud_id) REFERENCES aud_tbl(id)
);

CREATE INDEX aud_meta_aud_id_idx ON aud_meta_tbl(aud_id);

ALTER TABLE aud_obj_tbl ALTER COLUMN obj_id TYPE VARCHAR(512);