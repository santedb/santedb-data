/** 
 * <feature scope="SanteDB.Persistence.Data" id="20250127-01" name="Update:20250127-01"   invariantName="FirebirdSQL">
 *	<summary>Update: Adds bundle tracking to the database</summary>
 *	<isInstalled>select ck_patch('20250127-01') from rdb$database</isInstalled>
 * </feature>
 */

 CREATE TABLE bdl_corr_systbl (
	corr_id UUID NOT NULL,
	CONSTRAINT pk_bdl_corr_systbl PRIMARY KEY (corr_id)
 );

--#!

CREATE TABLE bdl_corr_subm_systbl (
	subm_id UUID NOT NULL, 
	corr_id UUID NOT NULL,
	seq BIGINT NOT NULL,
	crt_prov_id UUID NOT NULL,
	crt_utc TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
	CONSTRAINT PK_bdl_corr_subm_systbl PRIMARY KEY (subm_id),
	CONSTRAINT FK_bdl_corr_subm_corr_systbl FOREIGN KEY (corr_id) REFERENCES bdl_corr_systbl(corr_id),
	CONSTRAINT FK_bdl_corr_subm_crt_prov_id FOREIGN KEY (crt_prov_id) REFERENCES sec_prov_tbl(prov_id)
);
--#!

CREATE INDEX bdl_corr_subm_corr_id_idx ON bdl_corr_subm_systbl(corr_id);

--#!

 SELECT REG_PATCH('20250127-01') FROM RDB$DATABASE;--#! 