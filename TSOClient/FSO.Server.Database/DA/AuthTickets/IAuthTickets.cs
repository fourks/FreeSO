﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSO.Server.Database.DA.AuthTickets
{
    public interface IAuthTickets
    {
        void Create(AuthTicket ticket);
    }
}
