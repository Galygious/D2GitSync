using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D2RLAN.Models.Enums
{
    public enum eItemDisplay
    {
        [Display(Name = "No Icons")]
        NoIcons,
        [Display(Name = "Item + Runes")]
        ItemRuneIcons,
        [Display(Name = "Items Only")]
        ItemIconsOnly,
        [Display(Name = "Runes Only")]
        RuneIconsOnly,
    }
}
