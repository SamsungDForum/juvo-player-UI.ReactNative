using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using JuvoLogger;
using JuvoLogger.Tizen;
using ReactNative;
using ReactNative.Bridge;
using ReactNative.Shell;
using ReactNative.Modules.Core;

using Tizen.Applications;
using UI.Common;
using PlayerService;

namespace JuvoReactNative
{
    class ReactNativeApp : ReactProgram, IDeepLinkSender
    {

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
                LogRn.Info("Packages loading...");
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

                LogRn.Error(e.Message);
                LogRn.Error(e.StackTrace);
            }
            else
            {
                LogRn.Error("Got unhandled exception event: " + evt);
            }
        }
        protected override void OnCreate()
        {

            LogRn.Debug("OnCreate()");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ServicePointManager.DefaultConnectionLimit = 100;
            base.OnCreate();
            RootView.BackgroundColor = ElmSharp.Color.Transparent;

            LogRn.Debug("OnCreate() done");
        }

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            LogRn.Debug("OnAppControlReceived()");

            var payloadParser = new PayloadParser(e.ReceivedAppControl);
            if (!payloadParser.TryGetUrl(out var url))
                url = string.Empty;

            deepLinkReceivedSubject.OnNext(url);

            base.OnAppControlReceived(e);

            LogRn.Debug("OnAppControlReceived() done");
        }

        protected override void OnTerminate()
        {
            LogRn.Debug("OnTerminate()");

            deepLinkReceivedSubject.OnCompleted();
            deepLinkReceivedSubject.Dispose();
            base.OnTerminate();

            LogRn.Debug("OnTerminate() done");
        }


        public IObservable<string> DeepLinkReceived()
        {
            return deepLinkReceivedSubject.AsObservable();
        }

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;

            Log.Logger = new LoggerBuilder()
                .WithLevel(LogLevel.Debug)
                .WithChannel("JuvoPlayer")
                .WithTizenSink()
                .Build();

            JuvoPlayer.Platforms.Tizen.PlatformTizen.Init();

            try
            {
                ReactNativeApp app = new ReactNativeApp();
                app.Run(args);
            }
            catch (Exception e)
            {
                LogRn.Error(e.ToString());
            }
        }
    }
}