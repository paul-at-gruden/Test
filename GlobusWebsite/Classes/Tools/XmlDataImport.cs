using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Publishing;
using Microsoft.XmlDiffPatch;

namespace GlobusWebsite.Classes.Tools
{
  public class XmlDataImport
  {
    private XmlDocument oDoc;
    private Boolean isValid = true;
    private static long lValidFiles = 0;
    private static long lInvalidFiles = 0;
    private Dictionary<string, string> dictInvalidFiles = new Dictionary<string, string>();
    
    private static Database dbMaster = Sitecore.Data.Database.GetDatabase("master");
    private static Database dbWeb = Sitecore.Data.Database.GetDatabase("web");
    private static DirectoryInfo inputDirectory = null;
    private static DirectoryInfo updateDirectory = null;
    private static DirectoryInfo currentDirectory = null;
    private static XmlDocument xmlDocToday = new XmlDocument();
    private static XmlDocument xmlCurrent = new XmlDocument();
    private static XmlDocument xmlDiffGraph = new XmlDocument();
    private static StreamReader strRead = null;

    private static PublishOptions pubOpts = new PublishOptions(dbMaster, dbWeb, PublishMode.Incremental, Sitecore.Globalization.Language.Predefined.English, System.DateTime.Now);

    private static ExtraNight oExtraNight = null;

    //Items
    private static Item itmTourRoot = dbMaster.GetItem(ConfigurationManager.AppSettings.Get("TourDataRoot"));
    private static Item itmTours = null;
    private static Item itmSeason = null;
    private static Item itmTourFolder = null;
    private static Item itmTour = null;
    private static Item itmItineraryFolder = null;
    private static Item itmItineraryDay = null;
    private static Item itmExtraNightsFolder = null;
    private static Item itmExtraNightsPreFolder = null;
    private static Item itmExtraNightsPostFolder = null;
    private static Item itmExtraNight = null;

    //Templates
    private static TemplateItem folderTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("FolderTemplate")));
    private static TemplateItem TourTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("TourTemplate")));
    private static TemplateItem ItineraryDayTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("ItineraryDayTemplate")));
    private static TemplateItem DateRangeTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("DateRangeTemplate")));
    
    private Boolean isValidXml = true;

    public void Run()
    {
      Stream strmDiff = null;
      DateTime dtStart = DateTime.Now;
      int iFileCount = 0;
      
      try
      {
        
        string strXmlInputRoot = ConfigurationManager.AppSettings.Get("XmlInputDirectory");
        inputDirectory = new DirectoryInfo(string.Format("{0}\\today", strXmlInputRoot));
        updateDirectory = new DirectoryInfo(string.Format("{0}\\update", strXmlInputRoot));
        currentDirectory = new DirectoryInfo(string.Format("{0}\\current", strXmlInputRoot));

        // delete all files in update directory
        updateDirectory.Empty();

        // get list of files in input & current directories
        FileInfo[] arrFiles = inputDirectory.GetFiles();
        string[] arrCurrentFiles = Directory.GetFiles(currentDirectory.FullName);
        writeLogInfo(string.Format("{0} files in today's folder", arrFiles.Length.ToString()));
        int iMaxFiles = arrFiles.Length;
        if (iMaxFiles > 50)
          iMaxFiles = 50;
        for (int i = 0; i < iMaxFiles; i++)
        {
          FileInfo fiFile = arrFiles[i];
          iFileCount++;
          writeLogInfo(fiFile.Name + " : " + iFileCount.ToString());
          if (validateXml(fiFile))
          //if (true)
          {
            //writeLogInfo(fiFile.Name + " is valid");
            //xmlDocToday.Load(fiFile.FullName);
            xmlDocToday = oDoc;
            if (!arrCurrentFiles.Contains<string>(string.Format("{0}\\{1}", currentDirectory.FullName, fiFile.Name)))
            {
              processWholeFile(fiFile, xmlDocToday);
            }
            else
            {
              xmlCurrent.Load(string.Format("{0}\\{1}", currentDirectory.FullName, fiFile.Name));
              strmDiff = new MemoryStream();
              XmlTextWriter diffGram = new XmlTextWriter(new StreamWriter(strmDiff));
              diffGram.Formatting = Formatting.Indented;
              XmlDiff diff = new XmlDiff(XmlDiffOptions.IgnoreChildOrder);
              diff.Compare(xmlCurrent, xmlDocToday, diffGram);
              strmDiff.Position = 0;
              strRead = new StreamReader(strmDiff);
              string strDiffGraph = strRead.ReadToEnd();
              xmlDiffGraph.LoadXml(strDiffGraph);
              if (xmlDiffGraph.ChildNodes[1].HasChildNodes)
              {
                //xmlDiffGraph.Save("c:\\temp\\diffGraph.xml");
                // differences exist so process file

              }
            }
          }
          fiFile.MoveTo(string.Format("{0}\\{1}", currentDirectory.FullName, fiFile.Name));
        }
        PublishTours(itmTourFolder);
      }
      catch (Exception ex)
      {
        logError(ex.ToString());
      }
      finally
      {
        // clean up resources
        if (strmDiff != null)
        {
          strmDiff.Close();
          strmDiff.Dispose();
        }
        if (strRead != null)
        {
          strRead.Close();
          strRead.Dispose();
        }
        writeLogInfo((DateTime.Now - dtStart).TotalSeconds.ToString());
      }
    }

    private Boolean validateXml(FileInfo fiFile)
    {
      isValid = true;
      string strDTDPath = "C:/Clients/Globus/GlobusWebsite/GlobusWebsite/XmlData/Tour.dtd";
        //HttpContext.Current.Server.MapPath("~/XmlData/Tour.dtd").Replace("\\", "/");

      try
      {

        oDoc = new XmlDocument();
        oDoc.Load(fiFile.FullName);
        oDoc.InsertBefore(oDoc.CreateDocumentType("Tour", null, String.Format("file:///{0}", strDTDPath), null), oDoc.DocumentElement);
        XmlReaderSettings settings = new XmlReaderSettings();
        //settings.ProhibitDtd = false;
        settings.DtdProcessing = DtdProcessing.Parse;
        settings.ValidationType = ValidationType.DTD;
        settings.ValidationEventHandler += new ValidationEventHandler(delegate(object sender, ValidationEventArgs args)
        {
          isValid = false;
          dictInvalidFiles.Add(fiFile.FullName.Substring(fiFile.FullName.LastIndexOf("\\") + 1), args.Message);
        });
        XmlReader validator = XmlReader.Create(new StringReader(oDoc.OuterXml), settings);
        while (validator.Read())
        {
        }
        validator.Close();

      }
      catch (Exception ex)
      {
        isValid = false;
      }

      lValidFiles += isValid ? 1 : 0;
      lInvalidFiles += isValid ? 0 : 1;

      return isValid;

    }

    static void vr_ValidationEventHandler(object sender, ValidationEventArgs e)
    {
      Console.WriteLine("***Validation error");
      Console.WriteLine("\tSeverity:{0}", e.Severity);
      Console.WriteLine("\tMessage  :{0}", e.Message);
    }

    private static void processWholeFile(FileInfo fiFile, XmlDocument xmlDoc)
    {
      Item itmCurrentTour = null;
      try
      {
        string strSeason = xmlDoc.SelectSingleNode("//Fields/Season").InnerText;
        string strProdCode = xmlDoc.SelectSingleNode("//Fields/ProdCode").InnerText;
        Dictionary<string, string> dictTourFields = new Dictionary<string, string>();
        dictTourFields.Add("Season", strSeason);
        dictTourFields.Add("Product Code", strProdCode);
        dictTourFields.Add("Name", xmlDoc.SelectSingleNode("//Fields/Name").InnerText);
        dictTourFields.Add("Tour Style", xmlDoc.SelectSingleNode("//Fields/TourStyle").InnerText);
        dictTourFields.Add("Duration", xmlDoc.SelectSingleNode("//Fields/Duration").InnerText);
        dictTourFields.Add("Start City", xmlDoc.SelectSingleNode("//Fields/StartCity").InnerText);
        dictTourFields.Add("End City", xmlDoc.SelectSingleNode("//Fields/EndCity").InnerText);
        dictTourFields.Add("Breakfast", xmlDoc.SelectSingleNode("//Fields/Breakfast").InnerText);
        dictTourFields.Add("Lunch", xmlDoc.SelectSingleNode("//Fields/Lunch").InnerText);
        dictTourFields.Add("Dinner", xmlDoc.SelectSingleNode("//Fields/Dinner").InnerText);
        dictTourFields.Add("Features", xmlDoc.SelectSingleNode("//Fields/Features").InnerText);
        itmCurrentTour = processTour(strSeason, strProdCode, dictTourFields);

        List<ItineraryDay> lstItineraryDays = new List<ItineraryDay>();
        XmlNodeList nlItineraryDays = xmlDoc.SelectNodes("/Tour/Itinerary/ItineraryDay");
        foreach (XmlNode ndDay in nlItineraryDays)
        {
          ItineraryDay newDay = new ItineraryDay();
          newDay.dayNumber = int.Parse(ndDay["DayNo"].InnerText);
          newDay.description = ndDay["Description"].InnerText;
          newDay.program = ndDay["Program"].InnerText;
          lstItineraryDays.Add(newDay);
        }
        processItineraryItems(itmCurrentTour, lstItineraryDays);

        
        XmlNodeList nlExtraNights = xmlDoc.SelectNodes("/Tour/ExtraNights/ExtraNight");
        if (nlExtraNights.Count > 0)
        {
          List<ExtraNight> lstExtraNights = new List<ExtraNight>();
          foreach (XmlNode ndExtraNight in nlExtraNights)
          {
            foreach(XmlNode ndDateRange in (ndExtraNight["DateRanges"]).SelectNodes("//DateRange"))
            {
              oExtraNight = new ExtraNight();
              oExtraNight.type = ndExtraNight["Type"].InnerText;
              oExtraNight.startDate = DateTime.Parse(ndDateRange["StartDate"].InnerText);
              oExtraNight.endDate = DateTime.Parse(ndDateRange["EndDate"].InnerText);
              lstExtraNights.Add(oExtraNight);
            }
            /*
            foreach (XmlNode ndPriceType in ndExtraNight["DateRanges"]["Price"].ChildNodes)
            {
              Price oPrice = new Price();
              oPrice.code = ndPriceType.Name;
              foreach (XmlNode ndPrice in ndPriceType.ChildNodes)
              {
                oPrice.amounts.Add(ndPrice.Name, int.Parse(ndPrice.InnerText));
              }
            }*/
            
          }
          processExtraNights(itmCurrentTour, lstExtraNights);
        
        }
        
        //fiFile.CopyTo(string.Format("{0}\\{1}", currentDirectory.FullName, fiFile.Name));
      }
      catch (Exception ex)
      {
        logError(ex.ToString());
      }
    }

    private static Item processTour(string season, string prodCode, Dictionary<string, string> fieldDictionary)
    {
      itmTours = getOrCreateItem("Tours", folderTemplate, itmTourRoot);
      itmSeason = getOrCreateItem(season, folderTemplate, itmTours);
      itmTourFolder = getOrCreateItem(prodCode.Substring(0, 1), folderTemplate, itmSeason);
      itmTour = getOrCreateItem(prodCode, TourTemplate, itmTourFolder);
      itmTour.Editing.BeginEdit();
      foreach (string strKey in fieldDictionary.Keys)
      {
        itmTour[strKey] = fieldDictionary[strKey];
      }
      itmTour.Editing.EndEdit();
      //PublishItem(itmTour);
      return itmTour;
    }

    private static void processItineraryItems(Item TourItem, List<ItineraryDay> itineraryDays)
    {
      itmItineraryFolder = getOrCreateItem("Itinerary", folderTemplate, TourItem);
      foreach (ItineraryDay itineraryDay in itineraryDays)
      {
        itmItineraryDay = getOrCreateItem(itineraryDay.dayNumber.ToString("000"), ItineraryDayTemplate, itmItineraryFolder);
        itmItineraryDay.Editing.BeginEdit();
        itmItineraryDay["Day Number"] = itineraryDay.dayNumber.ToString();
        itmItineraryDay["Description"] = itineraryDay.description;
        itmItineraryDay["Program"] = itineraryDay.program;
        itmItineraryDay.Editing.EndEdit();
        //PublishItem(itmItineraryDay);
      }      
    }

    private static void processExtraNights(Item TourItem, List<ExtraNight> extraNights)
    {
      itmExtraNightsFolder = getOrCreateItem("Extra Nights", folderTemplate, TourItem);
      itmExtraNightsPreFolder = getOrCreateItem("PRE", folderTemplate, itmExtraNightsFolder);
      itmExtraNightsPostFolder = getOrCreateItem("POST", folderTemplate, itmExtraNightsFolder);
      foreach (ExtraNight extraNight in extraNights)
      {
        if (extraNight.type == "PRE")
        {
          itmExtraNight = getOrCreateItem(extraNight.startDate.ToString("yyyy-MM-dd"), DateRangeTemplate, itmExtraNightsPreFolder);
        }
        else
        {
          itmExtraNight = getOrCreateItem(extraNight.startDate.ToString("yyyy-MM-dd"), DateRangeTemplate, itmExtraNightsPostFolder);
        }
        itmExtraNight.Editing.BeginEdit();
        itmExtraNight["Start Date"] = string.Format("{0}T000000", extraNight.startDate.ToString("yyyyMMdd"));
        itmExtraNight["End Date"] = string.Format("{0}T000000", extraNight.endDate.ToString("yyyyMMdd"));
        itmExtraNight.Editing.EndEdit();
        // process the prices
      }

      /*
      foreach (ItineraryDay itineraryDay in itineraryDays)
      {
        itmItineraryDay = getOrCreateItem(itineraryDay.dayNumber.ToString("000"), ItineraryDayTemplate, itmItineraryFolder);
        itmItineraryDay.Editing.BeginEdit();
        itmItineraryDay["Day Number"] = itineraryDay.dayNumber.ToString();
        itmItineraryDay["Description"] = itineraryDay.description;
        itmItineraryDay["Program"] = itineraryDay.program;
        itmItineraryDay.Editing.EndEdit();
        //PublishItem(itmItineraryDay);
      }
       */
    }
    private static Item getOrCreateItem(String itemName, TemplateItem itemTemplate, Item ParentItem)
    {
      Item itmNewItem = null;
      try
      {
        itmNewItem = ParentItem.Children[itemName];
        if (itmNewItem == null)
        {
          itmNewItem = ParentItem.Add(itemName, itemTemplate);
          //PublishItem(itmNewItem);
        }
      }
      catch (Exception ex)
      {
        logError(ex.ToString());
      }
      return itmNewItem;
    }

    private static void writeLogInfo(string strTextToWriteToLog)
    {
      Sitecore.Diagnostics.Log.Info(string.Format("**** {0}", strTextToWriteToLog), typeof(XmlDataImport));
    }

    private static void logError(string strTextToWriteToLog)
    {
      Sitecore.Diagnostics.Log.Error(string.Format("**** {0}", strTextToWriteToLog), typeof(XmlDataImport));
    }

    private static void PublishItem(Item itmToBePublished)
    {
      Publisher pub = new Publisher(pubOpts);
      pub.Options.RootItem = itmToBePublished;
      pub.Options.Deep = false;
      pub.Publish();
    }

    private static void PublishTours(Item itmToBePublished)
    {
      PublishOptions pubOptsTours = new PublishOptions(dbMaster, dbWeb, PublishMode.Smart, Sitecore.Globalization.Language.Predefined.English, System.DateTime.Now);
      pubOptsTours.RootItem = itmToBePublished;
      Publisher pub = new Publisher(pubOptsTours);
      pub.Options.Deep = true;
      pub.Publish();
    }


    public class ItineraryDay
    {
      public int dayNumber { get; set; }
      public string description { get; set; }
      public string program { get; set; }
    }

    public class ExtraNight
    {
      public string type { get; set; }
      public DateTime startDate { get; set; }
      public DateTime endDate { get; set; }
      public List<Price> prices { get; set; }
    }

    public class Price
    {
      public string code { get; set; }
      public Dictionary<string, int> amounts { get; set; }
    }
  }

  public static class DirectoryExtension
  {
    public static void Empty(this DirectoryInfo directory)
    {
      foreach (FileInfo file in directory.GetFiles()) file.Delete();
      foreach (DirectoryInfo subDir in directory.GetDirectories()) subDir.Delete(true);
    }
  }
}