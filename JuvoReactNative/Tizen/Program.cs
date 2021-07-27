using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactNative;
using ReactNative.Bridge;
using ReactNative.Shell;
using ReactNative.Modules.Core;
using Log = Tizen.Log;
using Tizen.Applications;
using UI.Common;


namespace JuvoReactNative
{
    class ReactNativeApp : ReactProgram, IDeepLinkSender
    {
        public static readonly string Tag = "JuvoRN";

        private ReplaySubject<string> deepLinkReceivedSubject = new ReplaySubject<string>(1);
        public override string MainComponentName
        {
            get
            {
                return "JuvoReactNative";
            }
        }
        public override string JavaScriptMainModuleName
        {
            get
            {
                return "index.tizen";
            }
        }
#if !DEBUGJS
        public override string JavaScriptBundleFile
        {
            get
            {
                return Application.Current.DirectoryInfo.SharedResource + "index.tizen.bundle";
            }
        }
#endif
        public override List<IReactPackage> Packages
        {
            get
            {
                Log.Info(Tag, "Packages loading...");
                return new List<IReactPackage>
                {
                    new MainReactPackage(),
                    new JuvoPlayerReactPackage(this)
                };
            }
        }
        public override bool UseDeveloperSupport
        {
            get
            {
#if DEBUGJS
                return true;
#else
                return false;
#endif
            }
        }
        static void UnhandledException(object sender, UnhandledExceptionEventArgs evt)
        {
            if (evt.ExceptionObject is Exception e)
            {
                if (e.InnerException != null)
                    e = e.InnerException;

                Log.Error(Tag, e.Message);
                Log.Error(Tag, e.StackTrace);
            }
            else
            {
                Log.Error(Tag, "Got unhandled exception event: " + evt);
            }
        }
        protected override void OnCreate()
        {

            Log.Debug(Tag, "OnCreate()");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ServicePointManager.DefaultConnectionLimit = 100;
            base.OnCreate();
            RootView.BackgroundColor = ElmSharp.Color.Transparent;

            Log.Debug(Tag, "OnCreate() done");
        }

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            Log.Debug(Tag, $"OnAppControlReceived()");

            

            var payloadParser = new PayloadParser(e.ReceivedAppControl);
            if (payloadParser.TryGetUrl(out var url))
                deepLinkReceivedSubject.OnNext(url);

            base.OnAppControlReceived(e);

            Log.Debug(Tag, "OnAppControlReceived() done");
        }

        protected override void OnTerminate()
        {
            Log.Debug(Tag, "OnTerminate()");

            deepLinkReceivedSubject.OnCompleted();
            deepLinkReceivedSubject.Dispose();
            base.OnTerminate();

            Log.Debug(Tag, "OnTerminate() done");
        }


        public IObservable<string> DeepLinkReceived()
        {
            return deepLinkReceivedSubject.AsObservable();
        }

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;

            JuvoLogger.Tizen.TizenLoggerManager.Configure();
            JuvoPlayer.Platforms.Tizen.PlatformTizen.Init();

            try
            {
                ReactNativeApp app = new ReactNativeApp();
                app.Run(args);
            }
            catch (Exception e)
            {
                Log.Error(Tag, e.ToString());
            }
        }
    }
}