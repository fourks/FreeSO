﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Server.Database.DA.Users;
using FSO.Server.Database.DA.AuthTickets;

namespace FSO.Server.Database.DA
{
    public class SqlDA : IDA
    {
        public ISqlContext Context;

        public SqlDA(ISqlContext context)
        {
            this.Context = context;
        }

        private IUsers _users;
        public IUsers Users
        {
            get
            {
                if(_users == null)
                {
                    _users = new SqlUsers(Context);
                }
                return _users;
            }
        }

        private IAuthTickets _authTickets;
        public IAuthTickets AuthTickets
        {
            get
            {
                if (_authTickets == null)
                {
                    _authTickets = new SqlAuthTickets(Context);
                }
                return _authTickets;
            }
        }

        public void Flush()
        {
            Context.Flush();
        }

        public void Dispose()
        {
            Context.Dispose();
        }
    }
}
