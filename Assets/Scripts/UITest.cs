using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UITest : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        //按下
        GetComponent<Image>().color = Color.red;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        //抬起
        GetComponent<Image>().color = Color.white;
    }
}