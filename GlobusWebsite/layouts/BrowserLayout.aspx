<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="BrowserLayout.aspx.cs" Inherits="GlobusWebsite.layouts.BrowserLayout" %>
<%@ Register TagPrefix="sc" Namespace="Sitecore.Web.UI.WebControls" Assembly="Sitecore.Analytics" %>
<!doctype html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
  <title><sc:FieldRenderer runat="server" ID="frBrowserTitle" FieldName="Title" /></title>
  <link rel="Shortcut Icon" href="../Content/images/Globus/favicon.ico" />
  <link href="../Content/bootstrap-responsive.min.css" rel="stylesheet" type="text/css" />
  <link href="../Content/bootstrap.min.css" rel="stylesheet" type="text/css" />
</head>
<body>
  <form id="form1" runat="server">
    <div class="container">
      <sc:Sublayout runat="server" id="subPageHeader" path="/sublayouts/PageHeader.ascx" />
      <div class="span12" style="margin: auto;">
        <sc:Placeholder key="main" runat="server" />
        <asp:Label ID="lblTest" runat="server" />
        <sc:Sublayout runat="server" id="subPageFooter" path="/sublayouts/PageFooter.ascx" />
      </div>
    </div>
  </form>
</body>
</html>
