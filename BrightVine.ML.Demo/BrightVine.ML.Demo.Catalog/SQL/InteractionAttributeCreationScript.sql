--=============================================================--
--============ Interaction Attribute Creation Script ==========--
--=============================================================--
declare @CHANGEAGENTID uniqueidentifier

exec DBO.USP_CHANGEAGENT_GETORCREATECHANGEAGENT
  @CHANGEAGENTID OUTPUT

if not exists (select 1
               from   DBO.ATTRIBUTECATEGORY
               where  ID = '46CBFF17-9A07-471D-8ADB-4E5C38CFAB9A')
  exec DBO.USP_DATAFORMTEMPLATE_ADD_ATTRIBUTECATEGORY
    @ID = '46CBFF17-9A07-471D-8ADB-4E5C38CFAB9A',-- Attribute Category
    @NAME = N'Sentiment',-- Attribute Category Name
    @ATTRIBUTERECORDTYPEID = 'A823C194-919F-45E3-A1CA-24D16A5E1DFF',-- Interaction Attribute Record
    @DATATYPECODE = N'0',-- Text Data Type
    @ONLYALLOWONEPERRECORD = 1,-- Allow One Value Per Record
    @CONSTITUENTSEARCHLISTCATALOGID = null,
    @CHANGEAGENTID = @CHANGEAGENTID 