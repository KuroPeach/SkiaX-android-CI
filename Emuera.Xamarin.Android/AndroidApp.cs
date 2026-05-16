using Xamarin.Forms;

namespace XEmuera.Droid
{
    public class AndroidApp : Application
    {
        public AndroidApp(Xamarin.Forms.View mainView)
        {
            MainPage = new ContentPage
            {
                Content = mainView,
            };
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
