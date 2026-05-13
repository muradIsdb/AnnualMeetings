import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { transportApi } from '../../api/transport';
import { TransportAssignVehicleModal } from '../../components/Transport/TransportAssignVehicleModal';

export const TransportDashboard: React.FC = () => {
  // State for the assign vehicle modal
  const [selectedGuest, setSelectedGuest] = useState<any | null>(null);
  const [isAssignModalOpen, setIsAssignModalOpen] = useState(false);

  // Queries for dashboard data
  const { data: fleetStatus } = useQuery({
    queryKey: ['dashboard/summary/transport'],
    queryFn: () => transportApi.getSummary()
  });

  const { data: guestsAll } = useQuery({
    queryKey: ['guests/transport-all'],
    queryFn: () => transportApi.getAllGuests()
  });

  const { data: guestsQueue } = useQuery({
    queryKey: ['guests/transport-queue'],
    queryFn: () => transportApi.getQueue()
  });

  const { data: guestsInTransit } = useQuery({
    queryKey: ['guests/transport-in-transit'],
    queryFn: () => transportApi.getInTransit()
  });

  const openAssignModal = (guest: any) => {
    setSelectedGuest(guest);
    setIsAssignModalOpen(true);
  };

  const closeAssignModal = () => {
    setSelectedGuest(null);
    setIsAssignModalOpen(false);
  };

  return (
    <div className="p-6">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Transportation</h1>
        <p className="text-gray-500">Vehicle dispatch & assignment</p>
      </div>

      {/* Fleet Status Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div className="bg-white p-4 rounded-lg shadow-sm border border-gray-100">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-medium text-gray-500">Vehicles Available</h3>
            <span className="text-green-500">🚗</span>
          </div>
          <p className="text-2xl font-bold text-gray-900 mt-2">{fleetStatus?.vehiclesAvailable || 0}</p>
          <p className="text-xs text-gray-500 mt-1">of {fleetStatus?.vehiclesTotal || 0} total</p>
        </div>
        
        <div className="bg-white p-4 rounded-lg shadow-sm border border-gray-100">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-medium text-gray-500">Vehicles Assigned</h3>
            <span className="text-blue-500">🚕</span>
          </div>
          <p className="text-2xl font-bold text-gray-900 mt-2">{fleetStatus?.vehiclesAssigned || 0}</p>
        </div>

        <div className="bg-white p-4 rounded-lg shadow-sm border border-gray-100">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-medium text-gray-500">Drivers Available</h3>
            <span className="text-purple-500">👤</span>
          </div>
          <p className="text-2xl font-bold text-gray-900 mt-2">{fleetStatus?.driversAvailable || 0}</p>
          <p className="text-xs text-gray-500 mt-1">of {fleetStatus?.driversTotal || 0} total</p>
        </div>

        <div className="bg-yellow-50 p-4 rounded-lg shadow-sm border border-yellow-100">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-medium text-yellow-800">Guests Without Vehicle</h3>
            <span className="text-yellow-600">👥</span>
          </div>
          <p className="text-2xl font-bold text-yellow-900 mt-2">{fleetStatus?.guestsWithoutVehicle || 0}</p>
        </div>
      </div>

      {/* Fleet by Class Section */}
      <div className="mb-8">
        <h2 className="text-lg font-semibold text-gray-900 mb-4 uppercase tracking-wider text-xs">Fleet by Class</h2>
        <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-4 gap-4">
          {fleetStatus?.classes?.map((carClass: any) => (
            <div key={carClass.id} className="bg-white p-4 rounded-lg shadow-sm border border-gray-100">
              <div className="flex items-center mb-3">
                <div className="w-3 h-3 rounded-full mr-2" style={{ backgroundColor: carClass.color || '#ccc' }}></div>
                <h3 className="font-medium text-gray-900">{carClass.name}</h3>
              </div>
              <div className="flex justify-between text-center">
                <div>
                  <p className="text-xl font-bold text-green-600">{carClass.available}</p>
                  <p className="text-xs text-gray-500">Available</p>
                </div>
                <div>
                  <p className="text-xl font-bold text-blue-600">{carClass.assigned}</p>
                  <p className="text-xs text-gray-500">Assigned</p>
                </div>
                <div>
                  <p className="text-xl font-bold text-gray-900">{carClass.guests}</p>
                  <p className="text-xs text-gray-500">Guests</p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Dispatch Overview Section */}
      <div className="mb-8">
        <h2 className="text-lg font-semibold text-gray-900 mb-4 uppercase tracking-wider text-xs">Dispatch Overview</h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-red-50 p-4 rounded-lg shadow-sm border border-red-100 text-center">
            <span className="text-red-500 mb-2 block">⚠️</span>
            <p className="text-2xl font-bold text-red-900">{guestsQueue?.length || 0}</p>
            <p className="text-sm font-medium text-red-800">Awaiting Dispatch</p>
            <p className="text-xs text-red-600 mt-1">Arrived, no vehicle</p>
          </div>
          <div className="bg-blue-50 p-4 rounded-lg shadow-sm border border-blue-100 text-center">
            <span className="text-blue-500 mb-2 block">🚗</span>
            <p className="text-2xl font-bold text-blue-900">{guestsInTransit?.length || 0}</p>
            <p className="text-sm font-medium text-blue-800">In Transit → Hotel</p>
            <p className="text-xs text-blue-600 mt-1">Vehicle assigned</p>
          </div>
          <div className="bg-gray-50 p-4 rounded-lg shadow-sm border border-gray-100 text-center">
            <span className="text-gray-500 mb-2 block">✈️</span>
            <p className="text-2xl font-bold text-gray-900">0</p>
            <p className="text-sm font-medium text-gray-800">Departing</p>
            <p className="text-xs text-gray-600 mt-1">Heading to airport</p>
          </div>
        </div>
      </div>

      {/* Main Lists Section */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
        {/* Priority Dispatch Queue */}
        <div>
          <h2 className="text-lg font-semibold text-red-800 mb-4 flex items-center">
            <span className="mr-2">⚠️</span> Priority Dispatch Queue ({guestsQueue?.length || 0})
          </h2>
          <div className="bg-white rounded-lg shadow-sm border border-red-100 p-4">
            <div className="mb-4">
              <input 
                type="text" 
                placeholder="Search guests..." 
                className="w-full px-3 py-2 border border-gray-300 rounded-md"
              />
            </div>
            <div className="space-y-3">
              {guestsQueue?.map((guest: any) => (
                <div key={guest.id} className="flex items-center justify-between p-3 bg-red-50 rounded-lg border border-red-100">
                  <div>
                    <div className="flex items-center">
                      <h4 className="font-medium text-gray-900 mr-2">{guest.fullName}</h4>
                      {guest.carClassName && (
                        <span className="px-2 py-0.5 text-xs rounded-full bg-red-100 text-red-800">
                          {guest.carClassName}
                        </span>
                      )}
                    </div>
                    <p className="text-sm text-gray-600">{guest.designation}</p>
                    <p className="text-xs font-medium text-red-600 mt-1">⚠️ Arrived — Needs Vehicle</p>
                  </div>
                  <button 
                    onClick={() => openAssignModal(guest)}
                    className="px-4 py-2 bg-green-600 text-white text-sm font-medium rounded-md hover:bg-green-700"
                  >
                    🚗 Assign
                  </button>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* In Transit */}
        <div>
          <h2 className="text-lg font-semibold text-blue-800 mb-4 flex items-center">
            <span className="mr-2">🚗</span> In Transit → Hotel ({guestsInTransit?.length || 0})
          </h2>
          <div className="bg-white rounded-lg shadow-sm border border-blue-100 p-4">
            <div className="space-y-3">
              {guestsInTransit?.map((guest: any) => (
                <div key={guest.id} className="flex items-center justify-between p-3 bg-blue-50 rounded-lg border border-blue-100">
                  <div>
                    <div className="flex items-center">
                      <h4 className="font-medium text-gray-900 mr-2">{guest.fullName}</h4>
                      {guest.carClassName && (
                        <span className="px-2 py-0.5 text-xs rounded-full bg-red-100 text-red-800">
                          {guest.carClassName}
                        </span>
                      )}
                    </div>
                    <p className="text-sm text-gray-600">{guest.designation}</p>
                    <p className="text-xs font-medium text-blue-600 mt-1">→ Vehicle Assigned</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Render the Assign Vehicle Modal if open */}
      {isAssignModalOpen && selectedGuest && (
        <TransportAssignVehicleModal
          guestId={selectedGuest.id}
          guestName={selectedGuest.fullName}
          activeAssignment={selectedGuest.activeAssignment}
          deservedCarClassId={selectedGuest.carClassId}
          deservedCarClassName={selectedGuest.carClassName}
          deservedCarClassColor={selectedGuest.carClassColor}
          onClose={closeAssignModal}
        />
      )}
    </div>
  );
};
