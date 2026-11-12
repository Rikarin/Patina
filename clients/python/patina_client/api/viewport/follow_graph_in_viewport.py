from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.viewport_follow import ViewportFollow
from ...models.viewport_state import ViewportState
from typing import cast



def _get_kwargs(
    *,
    body: ViewportFollow,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "put",
        "url": "/api/v1/viewport/follow",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | ViewportState | None:
    if response.status_code == 200:
        response_200 = ViewportState.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | ViewportState]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportFollow,

) -> Response[ProblemDetails | ViewportState]:
    """ Keeps a graph on the viewport: its Output nodes, or with node the node its selection views, laid on
    the mesh again after each evaluation and each change of selection, until something else is laid
    there. A null graph stops.

    Args:
        body (ViewportFollow): Keeps a graph session on the viewport, shown again after each of
            its evaluations, until something else is laid
            on the mesh. The Server does the following, so a client that asks needs to do nothing more
            (#244).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ViewportState]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportFollow,

) -> ProblemDetails | ViewportState | None:
    """ Keeps a graph on the viewport: its Output nodes, or with node the node its selection views, laid on
    the mesh again after each evaluation and each change of selection, until something else is laid
    there. A null graph stops.

    Args:
        body (ViewportFollow): Keeps a graph session on the viewport, shown again after each of
            its evaluations, until something else is laid
            on the mesh. The Server does the following, so a client that asks needs to do nothing more
            (#244).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ViewportState
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportFollow,

) -> Response[ProblemDetails | ViewportState]:
    """ Keeps a graph on the viewport: its Output nodes, or with node the node its selection views, laid on
    the mesh again after each evaluation and each change of selection, until something else is laid
    there. A null graph stops.

    Args:
        body (ViewportFollow): Keeps a graph session on the viewport, shown again after each of
            its evaluations, until something else is laid
            on the mesh. The Server does the following, so a client that asks needs to do nothing more
            (#244).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ViewportState]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportFollow,

) -> ProblemDetails | ViewportState | None:
    """ Keeps a graph on the viewport: its Output nodes, or with node the node its selection views, laid on
    the mesh again after each evaluation and each change of selection, until something else is laid
    there. A null graph stops.

    Args:
        body (ViewportFollow): Keeps a graph session on the viewport, shown again after each of
            its evaluations, until something else is laid
            on the mesh. The Server does the following, so a client that asks needs to do nothing more
            (#244).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ViewportState
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
