// Control Room Dashboard - full implementation
// Bundle variable map:
//   N=React, s=jsx namespace, ae=useQuery, xe=useMutation, Ge=useQueryClient
//   Or=useAuthStore, U=UserRole, qt=useNavigate, oe=toast (Ye)
//   ga=dashboardApi, g6=departureStatsApi, Gn=vehiclesApi, _=axios
//   It=format(date-fns), l4=formatDistanceToNow
//   Ad=hotelsApi.getActive
//   Icons: $l=LayoutDashboard, vt=Users, Nl=Plane, Se=Car, ma=Bell, yt=RefreshCw
//          hd=Hotel, Hl=Truck, Ys=Clock, Ab=Activity, Kb=TrendingUp, kt=CircleCheckBig
//          kb=CircleAlert, be=TriangleAlert, fn=Tag, Sb=ConciergeBell, xg=Bus
//          Aa=ChevronRight, hn=Plus, Ns=MapPin, Mb=Info, Ys=Clock, fa=Download
//          As=Check, vn=ChevronDown, ha=Circle, En=CircleCheck, Tb=Hourglass

function Z4(){
  const{user:CRu}=Or(),isAdminCR=(CRu?.roles??[CRu?.role]).filter(Boolean).includes(U.Admin);
  const e=Ge();

  // Fetch dashboard summary (vehicles, guests, fleet)
  const{data:c,isLoading:d,refetch:u}=ae({queryKey:["dashboard","summary"],queryFn:ga.getSummary,refetchInterval:3e4});

  // Fetch hotel summary
  const[crHotelData,setCrHotelData]=N.useState(null),[crHotelLoading,setCrHotelLoading]=N.useState(!0);
  N.useEffect(()=>{
    setCrHotelLoading(!0);
    _.get("/dashboard/hotel-summary").then(r2=>{setCrHotelData(r2.data);setCrHotelLoading(!1);}).catch(()=>setCrHotelLoading(!1));
  },[]);

  // Fetch departure stats
  const{data:crDep,isLoading:crDepLoading}=ae({queryKey:["departure-stats","cr"],queryFn:g6,refetchInterval:3e4});

  // Fetch vehicles with status
  const{data:crVehicles=[],isLoading:crVehLoading}=ae({queryKey:["vehicles","all-with-status","cr"],queryFn:Gn.getAllWithStatus,refetchInterval:3e4});


  // Derived vehicle stats
  const crVehAssigned=crVehicles.filter(h=>h.currentGuestId).length;
  const crVehAvailable=crVehicles.filter(h=>!h.currentGuestId&&h.isActive&&!h.isOutOfService).length;
  const crVehOOS=crVehicles.filter(h=>h.isOutOfService).length;
  const crVehTotal=crVehicles.length;

  // Fleet by class from dashboard summary
  const crFleetByClass=c?.fleetByClass??[];

  // Hotel rows
  const crHotels=crHotelData?.hotels??[];
  const crTotalRooms=crHotels.reduce((acc,h)=>acc+(h.totalRooms??0),0);
  const crOccupied=crHotels.reduce((acc,h)=>acc+(h.occupiedRooms??0),0);

  // Departure stats
  const crDepTotal=crDep?.total??0;
  const crDepSubmitted=crDep?.submitted??0;
  const crDepPending=crDep?.pending??0;
  const crDepByHotel=crDep?.byHotel??[];

  // Guest stats from summary
  const crTotalGuests=c?.totalGuests??0;
  const crArrived=c?.arrivedCount??0;
  const crAtAirport=c?.arrivingCount??0;
  const crOnWay=c?.onTheWayToHotelCount??0;
  const crAtHotel=c?.atHotelCount??0;
  const crNotArrived=crTotalGuests-crArrived-crAtAirport-crOnWay;
  const crDeparting=c?.departingActiveCount??0;

  const crIsLoading=d||crHotelLoading||crDepLoading;

  // KPI card helper
  const CRKpi=({icon:Icon,value:val,label:lbl,sub:sub2,color:col="blue"})=>{
    const colorMap={blue:"border-blue-200 bg-blue-50 text-blue-700",green:"border-green-200 bg-green-50 text-green-700",amber:"border-amber-200 bg-amber-50 text-amber-700",red:"border-red-200 bg-red-50 text-red-700",purple:"border-purple-200 bg-purple-50 text-purple-700",gray:"border-gray-200 bg-gray-50 text-gray-700",teal:"border-teal-200 bg-teal-50 text-teal-700"};
    const cc=colorMap[col]||colorMap.blue;
    return s.jsxs("div",{className:`rounded-2xl border p-4 flex flex-col gap-1 ${cc}`,children:[
      s.jsxs("div",{className:"flex items-center gap-2",children:[s.jsx(Icon,{className:"w-5 h-5 opacity-70"}),s.jsx("span",{className:"text-2xl font-bold",children:val??0})]}),
      s.jsx("p",{className:"text-sm font-medium",children:lbl}),
      sub2&&s.jsx("p",{className:"text-xs opacity-60",children:sub2})
    ]});
  };

  // Section header helper
  const CRSec=({title:st,icon:Icon})=>s.jsxs("div",{className:"flex items-center gap-2 mb-3",children:[
    s.jsx("div",{className:"w-8 h-8 rounded-lg bg-isdb-green/10 flex items-center justify-center",children:s.jsx(Icon,{className:"w-4 h-4 text-isdb-green"})}),
    s.jsx("h2",{className:"text-base font-semibold text-gray-800",children:st})
  ]});

  // Skeleton loader
  const CRSkel=({rows:rn=4})=>s.jsx("div",{className:`grid grid-cols-2 lg:grid-cols-${rn} gap-4`,children:[...Array(rn)].map((_2,yi)=>s.jsx("div",{className:"card animate-pulse h-20 bg-gray-100"},yi))});

  return s.jsxs("div",{className:"p-4 md:p-6 space-y-6",children:[

    // ── Header ──────────────────────────────────────────────────────────────
    s.jsxs("div",{className:"flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3",children:[
      s.jsxs("div",{className:"flex items-center gap-3",children:[
        s.jsx("div",{className:"w-10 h-10 rounded-xl bg-isdb-green/10 flex items-center justify-center",children:s.jsx($l,{className:"w-5 h-5 text-isdb-green"})}),
        s.jsxs("div",{children:[s.jsx("h1",{className:"text-xl font-bold text-gray-900",children:"Control Room"}),s.jsx("p",{className:"text-sm text-gray-500",children:"Real-time operations overview"})]})
      ]}),
      s.jsxs("div",{className:"flex items-center gap-2",children:[
        s.jsx("button",{onClick:()=>{u();},className:"btn-secondary p-2",children:s.jsx(yt,{className:"w-4 h-4"})})
      ]})
    ]}),



    // ── Section 1: Reception Overview ────────────────────────────────────────
    s.jsxs("div",{className:"card",children:[
      s.jsx(CRSec,{title:"Reception Overview",icon:vt}),
      crIsLoading?s.jsx(CRSkel,{rows:4}):s.jsx("div",{className:"grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-7 gap-3",children:[
        s.jsx(CRKpi,{icon:vt,value:crTotalGuests,label:"Total Guests",color:"gray"}),
        s.jsx(CRKpi,{icon:kt,value:crArrived,label:"Arrived",color:"green"}),
        s.jsx(CRKpi,{icon:Nl,value:crAtAirport,label:"At Airport",color:"blue"}),
        s.jsx(CRKpi,{icon:Se,value:crOnWay,label:"En Route",color:"amber"}),
        s.jsx(CRKpi,{icon:hd,value:crAtHotel,label:"At Hotel",color:"teal"}),
        s.jsx(CRKpi,{icon:Tb,value:crNotArrived>0?crNotArrived:0,label:"Not Yet Arrived",color:"purple"}),
        s.jsx(CRKpi,{icon:us,value:crDeparting,label:"Departing Active",color:"red"})
      ]})
    ]}),

    // ── Section 2: Guest Status Breakdown ────────────────────────────────────
    c?.guestsByStatus&&c.guestsByStatus.length>0&&s.jsxs("div",{className:"card",children:[
      s.jsx(CRSec,{title:"Status Breakdown",icon:Ab}),
      s.jsx("div",{className:"grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3",children:c.guestsByStatus.map(h=>s.jsxs("div",{className:"rounded-xl border border-gray-200 bg-gray-50 p-3",children:[
        s.jsx("p",{className:"text-xl font-bold text-gray-900",children:h.count}),
        s.jsx("p",{className:"text-xs text-gray-500 mt-0.5",children:h.statusLabel.replace(/([A-Z])/g," $1").trim()})
      ]},h.statusLabel))})
    ]}),

    // ── Section 3: Vehicle Allocation ────────────────────────────────────────
    s.jsxs("div",{className:"card",children:[
      s.jsx(CRSec,{title:"Vehicle Allocation",icon:Se}),
      crIsLoading?s.jsx(CRSkel,{rows:4}):s.jsxs(s.Fragment,{children:[
        s.jsx("div",{className:"grid grid-cols-2 sm:grid-cols-4 gap-3 mb-4",children:[
          s.jsx(CRKpi,{icon:Hl,value:crVehTotal,label:"Total Fleet",color:"gray"}),
          s.jsx(CRKpi,{icon:kt,value:crVehAssigned,label:"Assigned",color:"green"}),
          s.jsx(CRKpi,{icon:ha,value:crVehAvailable,label:"Available",color:"blue"}),
          s.jsx(CRKpi,{icon:be,value:crVehOOS,label:"Out of Service",color:"red"})
        ]}),
        crFleetByClass.length>0&&s.jsxs("div",{children:[
          s.jsx("h3",{className:"text-sm font-medium text-gray-600 mb-2",children:"Fleet by Car Class"}),
          s.jsx("div",{className:"overflow-x-auto",children:s.jsxs("table",{className:"w-full text-sm",children:[
            s.jsx("thead",{children:s.jsxs("tr",{className:"border-b border-gray-200",children:[
              s.jsx("th",{className:"text-left py-2 px-3 text-gray-500 font-medium",children:"Class"}),
              s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Total"}),
              s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Assigned"}),
              s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Available"}),
              s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"OOS"}),
              s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Util %"})
            ]})}),
            s.jsx("tbody",{children:crFleetByClass.map((h,yi)=>s.jsxs("tr",{className:`border-b border-gray-100 ${yi%2===0?"bg-white":"bg-gray-50"}`,children:[
              s.jsx("td",{className:"py-2 px-3 font-medium text-gray-900",children:h.className??h.carClassName??"—"}),
              s.jsx("td",{className:"py-2 px-3 text-right text-gray-700",children:h.total??0}),
              s.jsx("td",{className:"py-2 px-3 text-right text-green-700 font-medium",children:h.assigned??0}),
              s.jsx("td",{className:"py-2 px-3 text-right text-blue-700",children:h.available??0}),
              s.jsx("td",{className:"py-2 px-3 text-right text-red-600",children:h.outOfService??0}),
              s.jsx("td",{className:"py-2 px-3 text-right",children:h.total>0?`${Math.round(((h.assigned??0)/(h.total??1))*100)}%`:"—"})
            ]},h.className??yi))})
          ]})})])
      ]})
    ]}),

    // ── Section 4: Accommodation ─────────────────────────────────────────────
    s.jsxs("div",{className:"card",children:[
      s.jsx(CRSec,{title:"Accommodation",icon:hd}),
      crHotelLoading?s.jsx(CRSkel,{rows:3}):s.jsxs(s.Fragment,{children:[
        s.jsx("div",{className:"grid grid-cols-2 sm:grid-cols-3 gap-3 mb-4",children:[
          s.jsx(CRKpi,{icon:hd,value:crHotels.length,label:"Active Hotels",color:"teal"}),
          s.jsx(CRKpi,{icon:kt,value:crOccupied,label:"Occupied Rooms",color:"green"}),
          s.jsx(CRKpi,{icon:ha,value:crTotalRooms-crOccupied,label:"Available Rooms",color:"blue"})
        ]}),
        crHotels.length>0&&s.jsx("div",{className:"overflow-x-auto",children:s.jsxs("table",{className:"w-full text-sm",children:[
          s.jsx("thead",{children:s.jsxs("tr",{className:"border-b border-gray-200",children:[
            s.jsx("th",{className:"text-left py-2 px-3 text-gray-500 font-medium",children:"Hotel"}),
            s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Total Guests"}),
            s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Checked In"}),
            s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Expected"}),
            s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Total Rooms"}),
            s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Occupied"})
          ]})}),
          s.jsx("tbody",{children:crHotels.map((h,yi)=>s.jsxs("tr",{className:`border-b border-gray-100 ${yi%2===0?"bg-white":"bg-gray-50"}`,children:[
            s.jsx("td",{className:"py-2 px-3 font-medium text-gray-900",children:h.hotelName??"—"}),
            s.jsx("td",{className:"py-2 px-3 text-right text-gray-700",children:h.totalGuests??0}),
            s.jsx("td",{className:"py-2 px-3 text-right text-green-700 font-medium",children:h.checkedIn??0}),
            s.jsx("td",{className:"py-2 px-3 text-right text-blue-700",children:h.expected??0}),
            s.jsx("td",{className:"py-2 px-3 text-right text-gray-600",children:h.totalRooms??0}),
            s.jsx("td",{className:"py-2 px-3 text-right text-gray-600",children:h.occupiedRooms??0})
          ]},h.hotelId??yi))})
        ]})})])
    ]}),

    // ── Section 5: Departure Forms ───────────────────────────────────────────
    s.jsxs("div",{className:"card",children:[
      s.jsx(CRSec,{title:"Departure Forms",icon:us}),
      crDepLoading?s.jsx(CRSkel,{rows:3}):s.jsxs(s.Fragment,{children:[
        s.jsx("div",{className:"grid grid-cols-2 sm:grid-cols-3 gap-3 mb-4",children:[
          s.jsx(CRKpi,{icon:vt,value:crDepTotal,label:"Total Guests",color:"gray"}),
          s.jsx(CRKpi,{icon:kt,value:crDepSubmitted,label:"Forms Submitted",color:"green"}),
          s.jsx(CRKpi,{icon:Tb,value:crDepPending,label:"Pending",color:"amber"})
        ]}),
        crDepByHotel.length>0&&s.jsxs("div",{children:[
          s.jsx("h3",{className:"text-sm font-medium text-gray-600 mb-2",children:"By Hotel"}),
          s.jsx("div",{className:"overflow-x-auto",children:s.jsxs("table",{className:"w-full text-sm",children:[
            s.jsx("thead",{children:s.jsxs("tr",{className:"border-b border-gray-200",children:[
              s.jsx("th",{className:"text-left py-2 px-3 text-gray-500 font-medium",children:"Hotel"}),
              s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Total"}),
              s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Submitted"}),
              s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Pending"}),
              s.jsx("th",{className:"text-right py-2 px-3 text-gray-500 font-medium",children:"Rate %"})
            ]})}),
            s.jsx("tbody",{children:crDepByHotel.map((h,yi)=>s.jsxs("tr",{className:`border-b border-gray-100 ${yi%2===0?"bg-white":"bg-gray-50"}`,children:[
              s.jsx("td",{className:"py-2 px-3 font-medium text-gray-900",children:h.hotelName??"—"}),
              s.jsx("td",{className:"py-2 px-3 text-right text-gray-700",children:h.total??0}),
              s.jsx("td",{className:"py-2 px-3 text-right text-green-700 font-medium",children:h.submitted??0}),
              s.jsx("td",{className:"py-2 px-3 text-right text-amber-700",children:h.pending??0}),
              s.jsx("td",{className:"py-2 px-3 text-right",children:h.total>0?`${Math.round(((h.submitted??0)/(h.total??1))*100)}%`:"—"})
            ]},h.hotelName??yi))})
          ]})})])
      ]})
    ]}),

    // ── Section 6: Recent Activity ───────────────────────────────────────────
    c?.recentActivity&&c.recentActivity.length>0&&s.jsxs("div",{className:"card",children:[
      s.jsx(CRSec,{title:"Recent Activity",icon:Ab}),
      s.jsx("div",{className:"space-y-2 max-h-80 overflow-y-auto pr-1",children:c.recentActivity.map((h,yi)=>s.jsxs("div",{className:"flex items-start gap-3 p-3 bg-gray-50 rounded-xl",children:[
        s.jsx("div",{className:`mt-0.5 w-2 h-2 rounded-full flex-shrink-0 ${h.type==="VehicleAssigned"?"bg-isdb-green":"bg-gray-400"}`}),
        s.jsxs("div",{className:"flex-1 min-w-0",children:[
          s.jsx("p",{className:"text-sm font-medium text-gray-900 truncate",children:h.guestName}),
          s.jsxs("p",{className:"text-xs text-gray-500",children:[h.type==="VehicleAssigned"?"Vehicle assigned":"Vehicle unassigned",h.vehiclePlate&&` · ${h.vehiclePlate}`,h.driverName&&` · ${h.driverName}`]}),
          h.detail&&s.jsx("p",{className:"text-xs text-gray-400 mt-0.5",children:h.detail})
        ]}),
        s.jsxs("div",{className:"flex items-center gap-1 text-xs text-gray-400 flex-shrink-0",children:[s.jsx(Ys,{className:"w-3 h-3"}),l4(new Date(h.occurredAt),{addSuffix:!0})]})
      ]},yi))})
    ]}),


  ]});
}
