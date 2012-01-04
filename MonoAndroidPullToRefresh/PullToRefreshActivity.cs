using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using MonoAndroid.PullToRefresh;

namespace MonoAndroidPullToRefresh
{
  [Activity(Label = "MonoAndroidPullToRefresh", MainLauncher = true, Icon = "@drawable/icon")]
  public class MyList : ListActivity
  {
    private String[] mStrings = {
            "Abbaye de Belloc", "Abbaye du Mont des Cats", "Abertam",
            "Abondance", "Ackawi", "Acorn", "Adelost", "Affidelice au Chablis",
            "Afuega'l Pitu", "Airag", "Airedale", "Aisy Cendre",
            "Allgauer Emmentaler"};

    private List<string> mListItems;

    protected override void OnCreate(Bundle bundle)
    {
      base.OnCreate(bundle);

      SetContentView(Resource.Layout.pulltorefreshlist);

      mListItems = new List<string>();
      foreach (string item in mStrings)
        mListItems.Add(item);

      ArrayAdapter<string> adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleListItem1, mListItems);
      ListAdapter = adapter;

      PullToRefreshListView plv = ListView as PullToRefreshListView;

      plv.SetOnRefreshListener(new RefreshListener(this));

      // Create your application here
    }

    void Populate()
    {     
      ArrayAdapter<string> adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleListItem1, mListItems);
      ListAdapter = adapter;      
    }


    class RefreshListener : PullToRefreshListView.OnRefreshListener
    {
      private MyList mylist;

      public RefreshListener(MyList list)
      {
        mylist = list;
      }

      public void onRefresh()
      {
        ThreadPool.QueueUserWorkItem(delegate
        {          
          PullToRefreshListView plv = mylist.ListView as PullToRefreshListView;

          Thread.Sleep(2000);

          mylist.mListItems.Insert(0, "Added after refresh...");

          mylist.RunOnUiThread(delegate
          {
            mylist.Populate();
            plv.onRefreshComplete();
          });
        });

      }
    }

    
  }
}