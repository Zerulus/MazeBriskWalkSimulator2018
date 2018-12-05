// This class contains some helper functions.
using UnityEngine;
using UnityEngine.Rendering;
using Mirror;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;

public class Utils
{

     // ScaleFloatToUShort( -1f, -1f, 1f, ushort.MinValue, ushort.MaxValue) => 0
    // ScaleFloatToUShort(  0f, -1f, 1f, ushort.MinValue, ushort.MaxValue) => 32767
    // ScaleFloatToUShort(0.5f, -1f, 1f, ushort.MinValue, ushort.MaxValue) => 49151
    // ScaleFloatToUShort(  1f, -1f, 1f, ushort.MinValue, ushort.MaxValue) => 65535
    public static ushort ScaleFloatToUShort(float value, float minValue, float maxValue, ushort minTarget, ushort maxTarget)
    {
        // note: C# ushort - ushort => int, hence so many casts
        int targetRange = maxTarget - minTarget; // max ushort - min ushort > max ushort. needs bigger type.
        float valueRange = maxValue - minValue;
        float valueRelative = value - minValue;
        return (ushort)(minTarget + (ushort)(valueRelative/valueRange * (float)targetRange));
    }

    // ScaleFloatToByte( -1f, -1f, 1f, byte.MinValue, byte.MaxValue) => 0
    // ScaleFloatToByte(  0f, -1f, 1f, byte.MinValue, byte.MaxValue) => 127
    // ScaleFloatToByte(0.5f, -1f, 1f, byte.MinValue, byte.MaxValue) => 191
    // ScaleFloatToByte(  1f, -1f, 1f, byte.MinValue, byte.MaxValue) => 255
    public static byte ScaleFloatToByte(float value, float minValue, float maxValue, byte minTarget, byte maxTarget)
    {
        // note: C# byte - byte => int, hence so many casts
        int targetRange = maxTarget - minTarget; // max byte - min byte only fits into something bigger
        float valueRange = maxValue - minValue;
        float valueRelative = value - minValue;
        return (byte)(minTarget + (byte)(valueRelative/valueRange * (float)targetRange));
    }

    // ScaleUShortToFloat(    0, ushort.MinValue, ushort.MaxValue, -1, 1) => -1
    // ScaleUShortToFloat(32767, ushort.MinValue, ushort.MaxValue, -1, 1) => 0
    // ScaleUShortToFloat(49151, ushort.MinValue, ushort.MaxValue, -1, 1) => 0.4999924
    // ScaleUShortToFloat(65535, ushort.MinValue, ushort.MaxValue, -1, 1) => 1
    public static float ScaleUShortToFloat(ushort value, ushort minValue, ushort maxValue, float minTarget, float maxTarget)
    {
        // note: C# ushort - ushort => int, hence so many casts
        float targetRange = maxTarget - minTarget;
        ushort valueRange = (ushort)(maxValue - minValue);
        ushort valueRelative = (ushort)(value - minValue);
        return minTarget + (float)((float)valueRelative/(float)valueRange * targetRange);
    }

    // ScaleByteToFloat(  0, byte.MinValue, byte.MaxValue, -1, 1) => -1
    // ScaleByteToFloat(127, byte.MinValue, byte.MaxValue, -1, 1) => -0.003921569
    // ScaleByteToFloat(191, byte.MinValue, byte.MaxValue, -1, 1) => 0.4980392
    // ScaleByteToFloat(255, byte.MinValue, byte.MaxValue, -1, 1) => 1
    public static float ScaleByteToFloat(byte value, byte minValue, byte maxValue, float minTarget, float maxTarget)
    {
        // note: C# byte - byte => int, hence so many casts
        float targetRange = maxTarget - minTarget;
        byte valueRange = (byte)(maxValue - minValue);
        byte valueRelative = (byte)(value - minValue);
        return minTarget + (float)((float)valueRelative/(float)valueRange * targetRange);
    }

    // useful to compress rotations where we only need X and Y and not Z, etc.
    // this allows for 0..16 per rotation component, which is still plenty in some cases
    public static byte PackTwoFloatsIntoByte(float u, float v, float minValue, float maxValue)
    {
        // pack each into 0xF, together they make 0xFF
        byte lower = ScaleFloatToByte(u, minValue, maxValue, 0x00, 0x0F);
        byte upper = ScaleFloatToByte(v, minValue, maxValue, 0x00, 0x0F);
        byte combined = (byte)(upper << 4 | lower);
        return combined;
    }

    // see PackTwoFloatsIntoByte for explanation
    public static float[] UnpackByteIntoTwoFloats(byte combined, float minTarget, float maxTarget)
    {
        byte lower = (byte)(combined & 0x0F);
        byte upper = (byte)((combined >> 4) & 0x0F);

        float u = ScaleByteToFloat(lower, 0x00, 0x0F, minTarget, maxTarget);
        float v = ScaleByteToFloat(upper, 0x00, 0x0F, minTarget, maxTarget);
        return new float[]{u, v};
    }

    // eulerAngles have 3 floats, putting them into 2 bytes of [x,y],[z,0]
    // would be a waste. instead we compress into 5 bits each => 15 bits.
    // so a ushort.
    public static ushort PackThreeFloatsIntoUShort(float u, float v, float w, float minValue, float maxValue)
    {
        // 5 bits max value = 1+2+4+8+16 = 31 = 0x1F
        byte lower = ScaleFloatToByte(u, minValue, maxValue, 0x00, 0x1F);
        byte middle = ScaleFloatToByte(v, minValue, maxValue, 0x00, 0x1F);
        byte upper = ScaleFloatToByte(w, minValue, maxValue, 0x00, 0x1F);
        ushort combined = (ushort)(upper << 10 | middle << 5 | lower);
        return combined;
    }

    // see PackThreeFloatsIntoUShort for explanation
    public static float[] UnpackUShortIntoThreeFloats(ushort combined, float minTarget, float maxTarget)
    {
        byte lower = (byte)(combined & 0x1F);
        byte middle = (byte)((combined >> 5) & 0x1F);
        byte upper = (byte)(combined >> 10); // nothing on the left, no & needed

        // note: we have to use 4 bits per float, so between 0x00 and 0x0F
        float u = ScaleByteToFloat(lower, 0x00, 0x1F, minTarget, maxTarget);
        float v = ScaleByteToFloat(middle, 0x00, 0x1F, minTarget, maxTarget);
        float w = ScaleByteToFloat(upper, 0x00, 0x1F, minTarget, maxTarget);
        return new float[]{u, v, w};
    }
    // Mathf.Clamp only works for float and int. we need some more versions:
    public static long Clamp(long value, long min, long max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    // is any of the keys UP?
    public static bool AnyKeyUp(KeyCode[] keys)
    {
        return keys.Any(k => Input.GetKeyUp(k));
    }

    // is any of the keys DOWN?
    public static bool AnyKeyDown(KeyCode[] keys)
    {
        return keys.Any(k => Input.GetKeyDown(k));
    }

    // is any of the keys PRESSED?
    public static bool AnyKeyPressed(KeyCode[] keys)
    {
        return keys.Any(k => Input.GetKey(k));
    }

    // detect headless mode (which has graphicsDeviceType Null)
    public static bool IsHeadless()
    {
        return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
    }

    // String.IsNullOrWhiteSpace that exists in NET4.5
    // note: can't be an extension because then it can't detect null strings
    //       like null.IsNullOrWhitespace
    public static bool IsNullOrWhiteSpace(string value)
    {
        return String.IsNullOrEmpty(value) || value.Trim().Length == 0;
    }

    // Distance between two ClosestPointOnBounds
    // this is needed in cases where entites are really big. in those cases,
    // we can't just move to entity.transform.position, because it will be
    // unreachable. instead we have to go the closest point on the boundary.
    //
    // Vector3.Distance(a.transform.position, b.transform.position):
    //    _____        _____
    //   |     |      |     |
    //   |  x==|======|==x  |
    //   |_____|      |_____|
    //
    //
    // Utils.ClosestDistance(a.collider, b.collider):
    //    _____        _____
    //   |     |      |     |
    //   |     |x====x|     |
    //   |_____|      |_____|
    //
    public static float ClosestDistance(Collider a, Collider b)
    {
        return Vector3.Distance(a.ClosestPointOnBounds(b.transform.position),
                                b.ClosestPointOnBounds(a.transform.position));
    }

    // raycast while ignoring self (by setting layer to "Ignore Raycasts" first)
    // => setting layer to IgnoreRaycasts before casting is the easiest way to do it
    // => raycast + !=this check would still cause hit.point to be on player
    // => raycastall is not sorted and child objects might have different layers etc.
    public static bool RaycastWithout(Ray ray, out RaycastHit hit, GameObject ignore)
    {
        // remember layers
        Dictionary<Transform, int> backups = new Dictionary<Transform, int>();

        // set all to ignore raycast
        foreach (Transform tf in ignore.GetComponentsInChildren<Transform>(true))
        {
            backups[tf] = tf.gameObject.layer;
            tf.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        // raycast
        bool result = Physics.Raycast(ray, out hit);

        // restore layers
        foreach (KeyValuePair<Transform, int> kvp in backups)
            kvp.Key.gameObject.layer = kvp.Value;

        return result;
    }

    // pretty print seconds as hours:minutes:seconds(.milliseconds/100)s
    public static string PrettySeconds(float seconds)
    {
        TimeSpan t = System.TimeSpan.FromSeconds(seconds);
        string res = "";
        if (t.Days > 0) res += t.Days + "d";
        if (t.Hours > 0) res += " " + t.Hours + "h";
        if (t.Minutes > 0) res += " " + t.Minutes + "m";
        // 0.5s, 1.5s etc. if any milliseconds. 1s, 2s etc. if any seconds
        if (t.Milliseconds > 0) res += " " + t.Seconds + "." + (t.Milliseconds / 100) + "s";
        else if (t.Seconds > 0) res += " " + t.Seconds + "s";
        // if the string is still empty because the value was '0', then at least
        // return the seconds instead of returning an empty string
        return res != "" ? res : "0s";
    }

    // hard mouse scrolling that is consistent between all platforms
    //   Input.GetAxis("Mouse ScrollWheel") and
    //   Input.GetAxisRaw("Mouse ScrollWheel")
    //   both return values like 0.01 on standalone and 0.5 on WebGL, which
    //   causes too fast zooming on WebGL etc.
    // normally GetAxisRaw should return -1,0,1, but it doesn't for scrolling
    public static float GetAxisRawScrollUniversal()
    {
        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (scroll < 0) return -1;
        if (scroll > 0) return  1;
        return 0;
    }

    // two finger pinch detection
    // source: https://docs.unity3d.com/Manual/PlatformDependentCompilation.html
    public static float GetPinch()
    {
        if (Input.touchCount == 2)
        {
            // Store both touches.
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            // Find the position in the previous frame of each touch.
            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            // Find the magnitude of the vector (the distance) between the touches in each frame.
            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            // Find the difference in the distances between each frame.
            return touchDeltaMag - prevTouchDeltaMag;
        }
        return 0;
    }

    // universal zoom: mouse scroll if mouse, two finger pinching otherwise
    public static float GetZoomUniversal()
    {
        if (Input.mousePresent)
            return Utils.GetAxisRawScrollUniversal();
        else if (Input.touchSupported)
            return GetPinch();
        return 0;
    }

    // find local player (clientsided)
    public static Player ClientLocalPlayer()
    {
        return ClientScene.localPlayer != null ? ClientScene.localPlayer.GetComponent<Player>() : null;
    }

    // parse first upper cased noun from a string, e.g.
    //   EquipmentWeaponBow => Equipment
    //   EquipmentShield => Equipment
    public static string ParseFirstNoun(string text)
    {
        MatchCollection matches = new Regex(@"([A-Z][a-z]*)").Matches(text);
        return matches.Count > 0 ? matches[0].Value : "";
    }

    // parse last upper cased noun from a string, e.g.
    //   EquipmentWeaponBow => Bow
    //   EquipmentShield => Shield
    public static string ParseLastNoun(string text)
    {
        MatchCollection matches = new Regex(@"([A-Z][a-z]*)").Matches(text);
        return matches.Count > 0 ? matches[matches.Count-1].Value : "";
    }

    // check if the cursor is over a UI or OnGUI element right now
    // note: for UI, this only works if the UI's CanvasGroup blocks Raycasts
    // note: for OnGUI: hotControl is only set while clicking, not while zooming
    public static bool IsCursorOverUserInterface()
    {
        // IsPointerOverGameObject check for left mouse (default)
        if (EventSystem.current.IsPointerOverGameObject())
            return true;

        // IsPointerOverGameObject check for touches
        for (int i = 0; i < Input.touchCount; ++i)
            if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                return true;

        // OnGUI check
        return GUIUtility.hotControl != 0;
    }

    // PBKDF2 hashing recommended by NIST:
    // http://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication800-132.pdf
    // salt should be at least 128 bits = 16 bytes
    public static string PBKDF2Hash(string text, string salt)
    {
        byte[] saltBytes = Encoding.UTF8.GetBytes(salt);
        Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(text, saltBytes, 10000);
        byte[] hash = pbkdf2.GetBytes(20);
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    // invoke multiple functions by prefix via reflection.
    // -> works for static classes too if object = null
    // -> cache it so it's fast enough for Update calls
    // -> C# only has Tuple support in 4.6, so we use KeyValuePair instead
    static Dictionary<KeyValuePair<Type,string>, MethodInfo[]> lookup = new Dictionary<KeyValuePair<Type,string>, MethodInfo[]>();
    public static MethodInfo[] GetMethodsByPrefix(Type type, string methodPrefix)
    {
        KeyValuePair<Type,string> key = new KeyValuePair<Type,string>(type, methodPrefix);
        if (!lookup.ContainsKey(key))
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                                       .Where(m => m.Name.StartsWith(methodPrefix))
                                       .ToArray();
            lookup[key] = methods;
        }
        return lookup[key];
    }

    public static void InvokeMany(Type type, object onObject, string methodPrefix, params object[] args)
    {
        foreach (MethodInfo method in GetMethodsByPrefix(type, methodPrefix))
            method.Invoke(onObject, args.ToArray());
    }
}
