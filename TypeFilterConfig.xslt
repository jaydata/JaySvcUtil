<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:edm="@@VERSIONNS@@"
                xmlns:m="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"
                xmlns:metadata="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"
                xmlns:annot="http://schemas.microsoft.com/ado/2009/02/edm/annotation"
                xmlns:exsl="http://exslt.org/common"
                xmlns:msxsl="urn:schemas-microsoft-com:xslt" exclude-result-prefixes="msxsl">

  <xsl:strip-space elements="property item unprocessed"/>
  <xsl:output method="text" indent="no"  />

  <xsl:template match="/">
    <xsl:for-each select="*/Type"  xml:space="default">
      <xsl:value-of select="@Name"/>
      <xsl:if test="count(Property) > 0">:</xsl:if>
      <xsl:for-each select="Property">
        <xsl:value-of select="@Name"/>
        <xsl:if test="position() != last()">,</xsl:if>
      </xsl:for-each>
      <xsl:text>;</xsl:text>
    </xsl:for-each>

  </xsl:template>
</xsl:stylesheet>
