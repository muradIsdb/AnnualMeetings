import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import {
  ArrowLeft, User, Phone, Mail, MapPin, Plane,
  CheckCircle, Circle, AlertTriangle, Accessibility, Car,
} from 'lucide-react'
import { guestsApi } from '../../api/services'
import { GuestStatus } from '../../types'
import { format } from 'date-fns'

const STATUS_STEPS = [
  { status: GuestStatus.ArrivedAtAirport, label: 'Arrived at Airport' },
  { status: GuestStatus.PassedPassportControl, label: 'Passport Control' },
  { status: GuestStatus.LuggageReceived, label: 'Luggage Received' },
  { status: GuestStatus.ReceivedByEmbassy, label: 'Received by Embassy' },
  { status: GuestStatus.OnTheWayToHotel, label: 'On the Way to Hotel' },
]

export default function GuestDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { data: guest, isLoading } = useQuery({
    queryKey: ['guest', id],
    queryFn: () => guestsApi.getById(id!),
    enabled: !!id,
    refetchInterval: 15_000,
  })

  const updateStatusMutation = useMutation({
    mutationFn: (status: GuestStatus) => guestsApi.updateStatus(id!, status),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['guest', id] })
      queryClient.invalidateQueries({ queryKey: ['guests'] })
      toast.success('Status updated successfully')
    },
    onError: () => toast.error('Failed to update status'),
  })

  const completeChecklistMutation = useMutation({
    mutationFn: (checklistItemId: string) => guestsApi.completeChecklistItem(id!, checklistItemId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['guest', id] })
      toast.success('Checklist item completed')
    },
    onError: () => toast.error('Failed to complete checklist item'),
  })

  if (isLoading) {
    return (
      <div className="p-6 space-y-4">
        {[...Array(4)].map((_, i) => (
          <div key={i} className="card animate-pulse h-24 bg-gray-100" />
        ))}
      </div>
    )
  }

  if (!guest) {
    return (
      <div className="p-6 text-center">
        <p className="text-gray-500">Guest not found.</p>
        <button onClick={() => navigate(-1)} className="btn-secondary mt-4">Go Back</button>
      </div>
    )
  }

  const arrivalFlight = guest.flights.find((f) => f.isArrival)
  const completedItems = guest.checklistCompletions.filter((c) => c.isCompleted)
  const progress = guest.checklistCompletions.length > 0
    ? Math.round((completedItems.length / guest.checklistCompletions.length) * 100)
    : 0

  return (
    <div className="p-6 space-y-5 max-w-2xl mx-auto">
      {/* Back button */}
      <button
        onClick={() => navigate(-1)}
        className="flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900"
      >
        <ArrowLeft className="w-4 h-4" />
        Back to Arrival Queue
      </button>

      {/* Guest header */}
      <div className="card flex items-start gap-4">
        {guest.photoUrl ? (
          <img src={guest.photoUrl} alt={guest.fullName} className="w-16 h-16 rounded-full object-cover flex-shrink-0" />
        ) : (
          <div className="w-16 h-16 rounded-full bg-isdb-green/10 flex items-center justify-center flex-shrink-0">
            <User className="w-8 h-8 text-isdb-green" />
          </div>
        )}
        <div className="flex-1">
          <div className="flex items-center gap-2 flex-wrap">
            <h1 className="text-xl font-bold text-gray-900">{guest.fullName}</h1>
            {guest.isCritical && <span className="badge-critical">VIP Critical</span>}
            {guest.requiresAccessibility && (
              <span className="flex items-center gap-1 text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full">
                <Accessibility className="w-3 h-3" /> Accessibility
              </span>
            )}
          </div>
          <p className="text-gray-600 mt-0.5">{guest.designation}</p>
          <p className="text-sm text-gray-500">{guest.organization}</p>
          <div className="flex items-center gap-4 mt-2 text-sm text-gray-500">
            {guest.mobileNumber && (
              <span className="flex items-center gap-1"><Phone className="w-3.5 h-3.5" />{guest.mobileNumber}</span>
            )}
            {guest.nationality && (
              <span className="flex items-center gap-1"><MapPin className="w-3.5 h-3.5" />{guest.nationality}</span>
            )}
          </div>
        </div>
      </div>

      {/* Flight info */}
      {arrivalFlight && (
        <div className="card">
          <div className="flex items-center gap-2 mb-3">
            <Plane className="w-4 h-4 text-blue-600" />
            <h3 className="font-semibold text-gray-900">Arrival Flight</h3>
          </div>
          <div className="grid grid-cols-2 gap-3 text-sm">
            <div>
              <p className="text-gray-500">Flight</p>
              <p className="font-semibold">{arrivalFlight.flightNumber}</p>
            </div>
            <div>
              <p className="text-gray-500">Status</p>
              <p className="font-semibold capitalize">{arrivalFlight.status || 'Scheduled'}</p>
            </div>
            {arrivalFlight.scheduledArrival && (
              <div>
                <p className="text-gray-500">Scheduled</p>
                <p className="font-semibold">{format(new Date(arrivalFlight.scheduledArrival), 'HH:mm, dd MMM')}</p>
              </div>
            )}
            {arrivalFlight.actualArrival && (
              <div>
                <p className="text-gray-500">Actual</p>
                <p className="font-semibold text-green-600">{format(new Date(arrivalFlight.actualArrival), 'HH:mm, dd MMM')}</p>
              </div>
            )}
            {arrivalFlight.delayMinutes && arrivalFlight.delayMinutes > 0 && (
              <div className="col-span-2">
                <span className="badge-warning">Delayed {arrivalFlight.delayMinutes} min</span>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Hotel info */}
      {guest.hotelName && (
        <div className="card">
          <div className="flex items-center gap-2 mb-2">
            <MapPin className="w-4 h-4 text-isdb-green" />
            <h3 className="font-semibold text-gray-900">Accommodation</h3>
          </div>
          <p className="text-sm text-gray-700">{guest.hotelName}</p>
          {guest.roomNumber && <p className="text-sm text-gray-500">Room {guest.roomNumber}</p>}
        </div>
      )}

      {/* Vehicle assignment */}
      {guest.activeVehicleAssignment && (
        <div className="card border-isdb-green/30 bg-isdb-green/5">
          <div className="flex items-center gap-2 mb-2">
            <Car className="w-4 h-4 text-isdb-green" />
            <h3 className="font-semibold text-gray-900">Assigned Vehicle</h3>
          </div>
          <p className="text-sm font-medium">{guest.activeVehicleAssignment.vehicleMake} {guest.activeVehicleAssignment.vehicleModel}</p>
          <p className="text-sm text-gray-600">{guest.activeVehicleAssignment.licensePlate}</p>
          {guest.activeVehicleAssignment.driverName && (
            <p className="text-sm text-gray-500">Driver: {guest.activeVehicleAssignment.driverName} · {guest.activeVehicleAssignment.driverPhone}</p>
          )}
        </div>
      )}

      {/* Arrival Checklist */}
      <div className="card">
        <div className="flex items-center justify-between mb-4">
          <h3 className="font-semibold text-gray-900">Arrival Checklist</h3>
          <span className="text-sm text-gray-500">{completedItems.length}/{guest.checklistCompletions.length} completed</span>
        </div>

        {/* Progress bar */}
        <div className="w-full bg-gray-100 rounded-full h-2 mb-4">
          <div
            className="bg-isdb-green h-2 rounded-full transition-all"
            style={{ width: `${progress}%` }}
          />
        </div>

        <div className="space-y-3">
          {guest.checklistCompletions
            .sort((a, b) => a.order - b.order)
            .map((item) => (
              <div
                key={item.checklistItemId}
                className={`flex items-center gap-3 p-3 rounded-lg ${
                  item.isCompleted ? 'bg-green-50' : 'bg-gray-50'
                }`}
              >
                <button
                  onClick={() => !item.isCompleted && completeChecklistMutation.mutate(item.checklistItemId)}
                  disabled={item.isCompleted || completeChecklistMutation.isPending}
                  className="flex-shrink-0"
                >
                  {item.isCompleted ? (
                    <CheckCircle className="w-5 h-5 text-green-600" />
                  ) : (
                    <Circle className="w-5 h-5 text-gray-400 hover:text-isdb-green transition-colors" />
                  )}
                </button>
                <div className="flex-1">
                  <p className={`text-sm font-medium ${item.isCompleted ? 'text-green-700 line-through' : 'text-gray-700'}`}>
                    {item.itemName}
                  </p>
                  {item.completedAt && (
                    <p className="text-xs text-gray-400">
                      {format(new Date(item.completedAt), 'HH:mm')} · {item.completedByName}
                    </p>
                  )}
                </div>
              </div>
            ))}
        </div>
      </div>

      {/* Status update actions */}
      <div className="card">
        <h3 className="font-semibold text-gray-900 mb-3">Update Status</h3>
        <div className="grid grid-cols-2 gap-2">
          {STATUS_STEPS.map((step) => (
            <button
              key={step.status}
              onClick={() => updateStatusMutation.mutate(step.status)}
              disabled={updateStatusMutation.isPending}
              className="btn-secondary text-sm text-left"
            >
              {step.label}
            </button>
          ))}
        </div>
      </div>

      {/* Special requirements */}
      {guest.specialRequirements && (
        <div className="card border-amber-200 bg-amber-50">
          <div className="flex items-center gap-2 mb-1">
            <AlertTriangle className="w-4 h-4 text-amber-600" />
            <h3 className="font-semibold text-amber-800">Special Requirements</h3>
          </div>
          <p className="text-sm text-amber-700">{guest.specialRequirements}</p>
        </div>
      )}
    </div>
  )
}
