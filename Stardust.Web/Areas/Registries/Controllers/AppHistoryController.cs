﻿using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Registries.Controllers
{
    [RegistryArea]
    public class AppHistoryController : EntityController<AppHistory>
    {
        static AppHistoryController()
        {
            MenuOrder = 93;
        }
    }
}