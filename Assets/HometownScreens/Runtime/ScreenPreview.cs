using UnityEngine;
namespace Hometown.Screens
{
    public class ScreenPreview : MonoBehaviour
    {
        public ReferenceScreen[] screens;
        public void Show(int index) { for(int i=0;i<screens.Length;i++) screens[i].gameObject.SetActive(i==index); }
    }
}
